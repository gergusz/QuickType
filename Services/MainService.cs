using System;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using QuickType.Model.Trie;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Text.Json;
using QuickType.Model;
using QuickType.Model.IPC;
using QuickType.Model.Languages;

namespace QuickType.Services;

public sealed partial class MainService(
    KeyboardCapturerService keyboardCapturerService,
    CaretFinderService caretFinderService,
    InputSimulatorService inputSimulatorService,
    SuggestionService suggestionService,
    LanguageService languageService,
    SettingsService settingsService,
    ILogger<MainService> logger) : BackgroundService
{

    private string _currentBuffer = string.Empty;

    private List<Word> _lastSuggestions = [];

    private const string PIPE_NAME = "QuickTypePipe";
    private NamedPipeServerStream? _pipeServer;
    private StreamWriter? _pipeStreamWriter;
    private StreamWriter? _statusPipeStreamWriter;
    private StreamReader? _pipeStreamReader;
    private Task? _pipeListenerTask;
    private CancellationTokenSource? _pipeListenerCancellationTokenSource;


    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await InitPipeServerAsync(stoppingToken);
            _pipeStreamReader = new StreamReader(_pipeServer);
            _pipeListenerCancellationTokenSource = new CancellationTokenSource();
            _pipeListenerTask = PipeListenerTaskAsync(_pipeListenerCancellationTokenSource.Token);

            await LoadLanguagesFromSettingsAsync();

            keyboardCapturerService.KeyboardEvent += KeyboardCapturer_KeyboardEvent;
            keyboardCapturerService.Start();
            logger.LogInformation("Keyboard capture started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Operation was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Message}", ex.Message);
            Environment.Exit(1);
        }
        finally
        {
            keyboardCapturerService.KeyboardEvent -= KeyboardCapturer_KeyboardEvent;
            keyboardCapturerService.Stop();

            _pipeServer?.Dispose();
            _pipeStreamWriter?.Dispose();
            _statusPipeStreamWriter?.Dispose();
            _pipeStreamReader?.Dispose();
            _pipeListenerCancellationTokenSource?.Cancel();

            logger.LogInformation("MainService stopped.");
        }
    }

    private void KeyboardCapturer_KeyboardEvent(string str)
    {
        if (languageService.LoadedLanguages.Count == 0)
        {
            logger.LogWarning("No languages loaded, ignoring keyboard event.");
            return;
        }

        try
        {
            ProcessKeyboardInput(str);

            if (_currentBuffer.Length > 1 && !string.IsNullOrWhiteSpace(str))
            {
                var caretRectangle = caretFinderService.GetCaretPos();
                var suggestions = suggestionService.GetSuggestions(languageService.LoadedLanguages, _currentBuffer, 
                    settingsService.AppSettings.IgnoreAccent, settingsService.AppSettings.MaxSuggestions);
                if (suggestions.Count > 0)
                {
                    _ = SendSuggestionsAsync(suggestions, caretRectangle);
                    _ = SendStatusMessageAsync("Javaslatok elküldve...");
                }
                else
                {
                    _ = SendStatusMessageAsync("Nem található javaslat!");
                    logger.LogInformation("No suggestions found for: {CurrentBuffer}", _currentBuffer);
                    if (keyboardCapturerService.AreSuggestionsShowing)
                    {
                        _ = SendSuggestionsCloseMessageAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing keyboard event: {Message}", ex.Message);
        }
    }
    private void ProcessKeyboardInput(string str)
    {
        switch (str)
        {
            case "\b" when _currentBuffer.Length <= 1:
                _currentBuffer = string.Empty;
                break;
            case "\b":
                _currentBuffer = _currentBuffer[..^1];
                break;
            case "\r":
            case " ":
            case "\n":
                _currentBuffer = string.Empty;
                _ = SendSuggestionsCloseMessageAsync();
                break;
            case not null when str.Contains(@"\c"):
                AcceptSuggestion((int)char.GetNumericValue(str[^1]), null, true); //intre cast, mert doublet ad vissza alapból (lásd ¼)
                _currentBuffer = string.Empty;
                _ = SendSuggestionsCloseMessageAsync();
                break;
            default:
                _currentBuffer += str;
                break;
        }
        logger.LogDebug("Current buffer: {CurrentBuffer}", _currentBuffer);
    }

    private async Task SendSuggestionsCloseMessageAsync()
    {
        if (_pipeServer is null || _pipeStreamWriter is null || !_pipeServer.IsConnected)
        {
            logger.LogWarning("Pipe server is not connected.");
            return;
        }

        try
        {
            if (!keyboardCapturerService.AreSuggestionsShowing)
            {
                return;
            }

            keyboardCapturerService.AreSuggestionsShowing = false;
            string json = JsonSerializer.Serialize(new CloseMessage());
            await _pipeStreamWriter.WriteLineAsync(json);
            logger.LogInformation("Sent close message to client.");
            await SendStatusMessageAsync("Javaslatok ablak bezárása...");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending close message: {Message}", ex.Message);
        }
        finally
        {
        }
    }

    private async Task PipeListenerTaskAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? line = await _pipeStreamReader.ReadLineAsync(cancellationToken);
                if (line != null)
                {
                    var ipcMessage = JsonSerializer.Deserialize<BaseIpcMessage>(line);

                    if (ipcMessage == null)
                    {
                        continue;
                    }

                    switch (ipcMessage.Type)
                    {
                        case IpcMessageType.SelectionMessage:
                            var selectionMessage = JsonSerializer.Deserialize<SelectionMessage>(line);
                            if (selectionMessage != null)
                            {
                                AcceptSuggestion(selectionMessage.Placement, selectionMessage.Word);
                                await SendSuggestionsCloseMessageAsync();
                            }

                            break;
                        case IpcMessageType.SettingsRequestMessage:
                            var settingsRequestMessage = JsonSerializer.Deserialize<SettingsRequestMessage>(line);
                            if (settingsRequestMessage != null)
                            {
                                if (settingsRequestMessage.Reset)
                                {
                                    logger.LogInformation("Settings reset requested.");
                                    await SendStatusMessageAsync("Beállítások visszaállítása eredeti értékekre...");
                                    await settingsService.ResetSettingsAsync();
                                    await SendStatusMessageAsync("Beállítások visszaállítva!");
                                }

                                await SendSettingsMessageAsync();
                            }
                            break;
                        case IpcMessageType.SettingsMessage:
                            var settingsMessage = JsonSerializer.Deserialize<SettingsMessage>(line);
                            if (settingsMessage != null)
                            {
                                var oldInternalLanguages = settingsService.AppSettings.LoadedInternalLanguages ?? [];
                                var newInternalLanguages = settingsMessage.Settings.LoadedInternalLanguages ?? [];
                                var oldCustomLanguages = settingsService.AppSettings.CustomLanguages ?? [];
                                var newCustomLanguages = settingsMessage.Settings.CustomLanguages ?? [];

                                var languageCompositionChanged = !oldInternalLanguages.Select(l => l.Name).OrderBy(n => n)
                                                                .SequenceEqual(newInternalLanguages.Select(l => l.Name).OrderBy(n => n)) ||
                                                               !oldCustomLanguages.Select(l => (l.Name, l.IsLoaded)).OrderBy(n => n.Name)
                                                                .SequenceEqual(newCustomLanguages.Select(l => (l.Name, l.IsLoaded)).OrderBy(n => n.Name));

                                if (languageCompositionChanged)
                                {
                                    logger.LogInformation("Language configuration changed, updating language service");

                                    var internalLanguagesToUnload = oldInternalLanguages
                                        .Where(ol => newInternalLanguages.All(nl => nl.Name != ol.Name))
                                        .ToList();

                                    var customLanguagesToUnload = oldCustomLanguages
                                        .Where(ol => ol.IsLoaded && (newCustomLanguages.All(nl => nl.Name != ol.Name) ||
                                                                     newCustomLanguages.FirstOrDefault(nl => nl.Name == ol.Name)?.IsLoaded == false))
                                        .ToList();

                                    if (internalLanguagesToUnload.Count > 0 || customLanguagesToUnload.Count > 0)
                                    {
                                        var internalNames = string.Join(", ", internalLanguagesToUnload.Select(x => x.Name));
                                        var customNames = string.Join(", ", customLanguagesToUnload.Select(x => x.Name));
                                        var languageNames = string.Join(", ", new[] { internalNames, customNames }.Where(s => !string.IsNullOrEmpty(s)));

                                        await SendStatusMessageAsync($"{languageNames} nyelv eltávolítása...");
                                        await languageService.UnloadLanguagesAsyncTask(internalLanguagesToUnload, customLanguagesToUnload);
                                        await SendStatusMessageAsync($"{languageNames} nyelv eltávolítva!");
                                    }
                                    else if (oldCustomLanguages.Count > newCustomLanguages.Count) //not to unload, but to remove
                                    {
                                        var languagesToDelete = oldCustomLanguages.Where(ol => !ol.IsLoaded && newCustomLanguages.All(nl => nl.Name != ol.Name))
                                            .ToList();

                                        await SendStatusMessageAsync($"{string.Join(", ", languagesToDelete.Select(x => x.Name))} nyelv törlése...");
                                        await languageService.DeleteLanguagesAsyncTask(languagesToDelete);
                                        await SendStatusMessageAsync($"{string.Join(", ", languagesToDelete.Select(x => x.Name))} nyelv törölve!");
                                    }

                                    var internalLanguagesToLoad = newInternalLanguages
                                        .Where(nl => oldInternalLanguages.All(ol => ol.Name != nl.Name))
                                        .ToList();

                                    var customLanguagesToLoad = newCustomLanguages
                                        .Where(nl => nl.IsLoaded && (oldCustomLanguages.All(ol => ol.Name != nl.Name) ||
                                                                     oldCustomLanguages.FirstOrDefault(ol => ol.Name == nl.Name)?.IsLoaded == false))
                                        .ToList();

                                    if (internalLanguagesToLoad.Count > 0 || customLanguagesToLoad.Count > 0)
                                    {
                                        var internalNames = string.Join(", ", internalLanguagesToLoad.Select(x => x.Name));
                                        var customNames = string.Join(", ", customLanguagesToLoad.Select(x => x.Name));
                                        var languageNames = string.Join(", ", new[] { internalNames, customNames }.Where(s => !string.IsNullOrEmpty(s)));

                                        await SendStatusMessageAsync($"{languageNames} nyelv betöltése...");
                                        await languageService.LoadLanguagesAsyncTask(internalLanguagesToLoad, customLanguagesToLoad);
                                        await SendStatusMessageAsync($"{languageNames} nyelv betöltve!");
                                    }
                                }
                                else
                                {
                                    var prioritiesChanged = !oldInternalLanguages.Select(x => (x.Name, x.Priority)).OrderBy(x => x.Name)
                                                            .SequenceEqual(newInternalLanguages.Select(x => (x.Name, x.Priority)).OrderBy(x => x.Name)) ||
                                                            !oldCustomLanguages.Where(l => l.IsLoaded).Select(x => (x.Name, x.Priority)).OrderBy(x => x.Name)
                                                            .SequenceEqual(newCustomLanguages.Where(l => l.IsLoaded).Select(x => (x.Name, x.Priority)).OrderBy(x => x.Name));

                                    if (prioritiesChanged)
                                    {
                                        logger.LogInformation("Language priorities changed, updating priorities");
                                        var priorityList = newInternalLanguages
                                            .Select(l => (l.Priority, l.Name))
                                            .Concat(newCustomLanguages.Where(l => l.IsLoaded).Select(l => (l.Priority, l.Name)))
                                            .ToList();
                                        await SendStatusMessageAsync("Prioritások módosítása...");
                                        languageService.ChangeLanguagePriorities(priorityList);
                                        await SendStatusMessageAsync("Prioritások módosítva!");
                                    }
                                }

                                await settingsService.HandleSettingsMessageAsync(settingsMessage.Settings);
                            }
                            break;
                        case IpcMessageType.RecreateLanguageDatabaseMessage:
                            var recreateMessage = JsonSerializer.Deserialize<RecreateLanguageDatabaseMessage>(line);
                            if (recreateMessage != null)
                            {
                                await SendStatusMessageAsync("Nyelvi adatbázis újragenerálása...");
                                await languageService.RecreateDatabaseOfLanguage(recreateMessage.Language);
                                await SendStatusMessageAsync("Nyelvi adatbázis újragenerálva!");
                            }
                            break;
                        case IpcMessageType.ServiceShutdownMessage:
                            var shutdownMessage = JsonSerializer.Deserialize<ServiceShutdownMessage>(line);
                            if (shutdownMessage != null)
                            {
                                await SendStatusMessageAsync("Szolgáltatás leállítása...");
                                Environment.Exit(0);
                            }
                            break;
                        default:
                            logger.LogWarning("Unknown message type: {Type}", ipcMessage.Type);
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in pipe listener task: {Message}", ex.Message);
        }
    }

    private async Task SendSettingsMessageAsync()
    {
        if (_pipeStreamWriter == null)
        {
            return;
        }

        try
        {
            var message = new SettingsMessage(settingsService.AppSettings);
            string json = JsonSerializer.Serialize(message);
            await _pipeStreamWriter.WriteLineAsync(json);
            logger.LogInformation("Sent settings through IPC");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending settings through IPC: {Message}", ex.Message);
        }
    }

    private void AcceptSuggestion(int? number, string? word, bool wasCtrlUsed = false)
    {
        if (number is null && word is null)
        {
            logger.LogError("Can't accept suggestion because number and word is both null");
            return;
        }

        if (number is not null)
        {
            if (settingsService.AppSettings.StartAtOne)
            {
                number--;
                if (number < 0)
                {
                    number = 9;
                }
            }
            if (_lastSuggestions.Count > number)
            {
                _ = SendStatusMessageAsync("Javaslat elfogadása...");
                var suggestion = _lastSuggestions[number.Value].word;
                inputSimulatorService.SimulateInputString(suggestion.RemoveFirst(_currentBuffer), wasCtrlUsed);
                _ = SendStatusMessageAsync("Javaslat elfogadva!");
                logger.LogInformation("Accepted suggestion: {Suggestion}", suggestion);
            }
            else
            {
                logger.LogWarning("Invalid suggestion index: {Number}", number);
            }
        } 
        else if (word is not null)
        {
            _ = SendStatusMessageAsync("Javaslat elfogadása...");
            inputSimulatorService.SimulateInputString(word.RemoveFirst(_currentBuffer), wasCtrlUsed);
            _ = SendStatusMessageAsync("Javaslat elfogadva!");
            logger.LogInformation("Accepted suggestion: {Suggestion}", word);
        }
        
    }
    private async Task LoadLanguagesFromSettingsAsync()
    {
        try
        {
            logger.LogInformation("Loading languages from settings...");

            var internalLanguages = settingsService.AppSettings.LoadedInternalLanguages ?? [];
            var customLanguages = settingsService.AppSettings.CustomLanguages ?? [];

            await languageService.LoadLanguagesAsyncTask(internalLanguages, customLanguages);

            logger.LogInformation("Languages loaded successfully: {Count} languages",
                languageService.LoadedLanguages.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading languages from settings: {Message}", ex.Message);
        }
    }

    private async Task InitPipeServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            _pipeServer = new NamedPipeServerStream(PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Message,
                PipeOptions.Asynchronous);

            logger.LogInformation("Pipe server created, waiting for connections...");

            await _pipeServer.WaitForConnectionAsync(cancellationToken);

            logger.LogInformation("Client connected to pipe.");
            _pipeStreamWriter = new StreamWriter(_pipeServer)
            {
                AutoFlush = true
            };

            _statusPipeStreamWriter = new StreamWriter(_pipeServer)
            {
                AutoFlush = true
            };
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Operation was cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error initializing pipe server: {Message}", ex.Message);
        }
    }

    private async Task SendSuggestionsAsync(List<Word> suggestions, CaretRectangle? caretPosition)
    {
        if (_pipeServer is null || _pipeStreamWriter is null || !_pipeServer.IsConnected)
        {
            logger.LogWarning("Pipe server is not connected.");
            return;
        }

        try
        {
            _lastSuggestions = suggestions;
            keyboardCapturerService.AreSuggestionsShowing = true;
            SuggestionMessage message = new(suggestions, caretPosition);
            string json = JsonSerializer.Serialize(message);
            await _pipeStreamWriter.WriteLineAsync(json);
            logger.LogInformation("Sent suggestions to client: {Suggestions}, {CaretPosition}", string.Join(", ", suggestions), caretPosition);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending suggestions: {Message}", ex.Message);
        }
    }

    private async Task SendStatusMessageAsync(string message)
    {
        if (_pipeServer is null || _statusPipeStreamWriter is null || !_pipeServer.IsConnected)
        {
            logger.LogWarning("Pipe server is not connected.");
            return;
        }
        try
        {
            StatusMessage statusMessage = new(message);
            string json = JsonSerializer.Serialize(statusMessage);
            await _statusPipeStreamWriter.WriteLineAsync(json);
            logger.LogInformation("Sent status message to client: {Message}", message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending status message: {Message}", ex.Message);
        }
    }
}