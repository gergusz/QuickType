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

namespace QuickType.Services;

public sealed partial class MainService(
    KeyboardCapturer keyboardCapturer,
    CaretFinder caretFinder,
    InputSimulator inputSimulator,
    SuggestionService suggestionService,
    LanguageService languageService,
    SettingsService settingsService,
    ILogger<MainService> logger) : BackgroundService
{

    private string currentBuffer = string.Empty;

    private List<Word> lastSuggestions = [];

    private const string PIPE_NAME = "QuickTypePipe";
    private NamedPipeServerStream? _pipeServer;
    private StreamWriter? _pipeStreamWriter;
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

            keyboardCapturer.KeyboardEvent += KeyboardCapturer_KeyboardEvent;
            keyboardCapturer.Start();
            logger.LogInformation("Keyboard capture started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                //await SendStatusMessageAsync("Healthy");

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
            keyboardCapturer.KeyboardEvent -= KeyboardCapturer_KeyboardEvent;
            keyboardCapturer.Stop();

            _pipeServer?.Dispose();
            _pipeStreamWriter?.Dispose();
            _pipeStreamReader?.Dispose();
            _pipeListenerCancellationTokenSource?.Cancel();

            logger.LogInformation("MainService stopped.");
        }
    }

    private void KeyboardCapturer_KeyboardEvent(string str)
    {
        try
        {
            ProcessKeyboardInput(str);

            if (currentBuffer.Length > 1 && !string.IsNullOrWhiteSpace(str))
            {
                var caretRectangle = caretFinder.GetCaretPos();
                var suggestions = suggestionService.GetSuggestions(languageService, currentBuffer,
                    settingsService.AppSettings.MaxSuggestions);
                if (suggestions.Count > 0)
                {
                    _ = SendSuggestionsAsync(suggestions, caretRectangle);
                }
                else
                {
                    logger.LogInformation("No suggestions found for: {CurrentBuffer}", currentBuffer);
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
            case "\b" when currentBuffer.Length <= 1:
                currentBuffer = string.Empty;
                break;
            case "\b":
                currentBuffer = currentBuffer[..^1];
                break;
            case "\r":
            case " ":
            case "\n":
                currentBuffer = string.Empty;
                _ = SendSuggestionsCloseMessageAsync();
                break;
            case not null when str.Contains(@"\c"):
                AcceptSuggestion((int)char.GetNumericValue(str[^1]), true); //intre cast, mert doublet ad vissza alapból (lásd ¼)
                currentBuffer = string.Empty;
                _ = SendSuggestionsCloseMessageAsync();
                break;
            default:
                currentBuffer += str;
                break;
        }
        logger.LogDebug("Current buffer: {CurrentBuffer}", currentBuffer);
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
            keyboardCapturer.AreSuggestionsShowing = false;
            string json = JsonSerializer.Serialize(new CloseMessage());
            await _pipeStreamWriter.WriteLineAsync(json);
            logger.LogInformation("Sent close message to client.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending close message: {Message}", ex.Message);
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
                                AcceptSuggestion(selectionMessage.Placement);
                                _ = SendSuggestionsCloseMessageAsync();
                            }

                            break;
                        case IpcMessageType.SettingsRequestMessage:
                            var settingsRequestMessage = JsonSerializer.Deserialize<SettingsRequestMessage>(line);
                            if (settingsRequestMessage != null)
                            {
                                if (settingsRequestMessage.Reset)
                                {
                                    await settingsService.ResetSettingsAsync();
                                }

                                await SendSettingsMessageAsync();
                            }
                            break;
                        case IpcMessageType.SettingsMessage:
                            var settingsMessage = JsonSerializer.Deserialize<SettingsMessage>(line);
                            if (settingsMessage != null)
                            {
                                settingsService.HandleSettingsMessageAsync(settingsMessage.Settings);
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

    public async Task SendSettingsMessageAsync()
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

    private void AcceptSuggestion(int number, bool wasCtrlUsed = false)
    {
        if (settingsService.AppSettings.StartAtOne)
        {
            number--;
            if (number < 0)
            {
                number = 9;
            }
        }
        if (lastSuggestions.Count > number)
        {
            string suggestion = lastSuggestions[number].word;
            inputSimulator.SimulateInputString(suggestion.RemoveFirst(currentBuffer), wasCtrlUsed);
            logger.LogInformation("Accepted suggestion: {Suggestion}", suggestion);
        }
        else
        {
            logger.LogWarning("Invalid suggestion index: {Number}", number);
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
            lastSuggestions = suggestions;
            keyboardCapturer.AreSuggestionsShowing = true;
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
        if (_pipeServer is null || _pipeStreamWriter is null || !_pipeServer.IsConnected)
        {
            logger.LogWarning("Pipe server is not connected.");
            return;
        }
        try
        {
            StatusMessage statusMessage = new(message);
            string json = JsonSerializer.Serialize(statusMessage);
            await _pipeStreamWriter.WriteLineAsync(json);
            logger.LogInformation("Sent status message to client: {Message}", message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error sending status message: {Message}", ex.Message);
        }
    }
}