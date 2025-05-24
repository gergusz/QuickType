using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using QuickType.Model;
using QuickType.Model.IPC;
using QuickType.Model.Languages;
using QuickType.Services;
using QuickType.WinUI;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App
{
    public MainWindow? MainWindow;
    public SuggestionsWindow? SuggestionsWindow; 
    public static new App? Current { get; private set; }

    private const string PIPE_NAME = "QuickTypePipe";
    private NamedPipeClientStream? _pipeClient;
    private StreamReader? _pipeStreamReader;
    private StreamWriter? _pipeStreamWriter;
    private CancellationTokenSource? _pipeListenerCancellationTokenSource;

    private IHost? _host;
    private readonly CancellationTokenSource _hostCancellationTokenSource = new();
    private bool _isServiceOnly;
    private Process? serviceProcess;

    //https://learn.microsoft.com/en-us/windows/win32/winmsg/about-messages-and-message-queues
    private const uint CALLBACK_MESSAGE_ID = 0x0400;
    private readonly Guid guid = Guid.NewGuid();

    private IntPtr _windowHandle;
    private WNDPROC? _wndProcDelegate;
    private const int ID_OPEN_SETTINGS = 1000;
    private const int ID_EXIT = 1001;
    private const string TASKBAR_CLASS_NAME = "QuickTypeTaskbarClass\0";

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        var args = Environment.GetCommandLineArgs();
        _isServiceOnly = args.Contains("--service") || args.Contains("-s");

        Current = this;
        //https://github.com/microsoft/microsoft-ui-xaml/issues/9658#issuecomment-2266183134
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
        this.InitializeComponent();

        this.UnhandledException += App_UnhandledException;
    }

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        Debug.WriteLine($"Unhandled exception: {e.Message}\n{e.Exception}");

        Task.Run(SendServiceShutdownAsync);

        var dispatcher = DispatcherQueue.GetForCurrentThread();
        dispatcher.TryEnqueue(async void () =>
        {
            var dialog = new ContentDialog
            {
                Title = "Hiba",
                Content = $"Váratlan hiba történt:\n{e.Exception.Message}",
                CloseButtonText = "Bezárás",
                XamlRoot = MainWindow?.Content.XamlRoot ?? SuggestionsWindow?.Content.XamlRoot
            };

            await dialog.ShowAsync();
            Current?.Exit();
        });

        e.Handled = true;
    }


    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        if (_isServiceOnly)
        {
            _ = StartHostAsync();
        }
        else
        {
            _ = InitPipeClientAsync();

            if (Debugger.IsAttached || (_pipeClient is null || !_pipeClient.IsConnected) && !TryLaunchServiceProcess())
            {
                _ = StartHostAsync();
            }

            CreateTaskBarIcon();

            MainWindow ??= new MainWindow();
            SuggestionsWindow ??= new SuggestionsWindow();
            MainWindow.Activate();
        }
    }

    private async Task StartHostAsync()
    {
        _host = Host.CreateDefaultBuilder().ConfigureServices((_, services) =>
        {
            services.AddSingleton<KeyboardCapturerService>();
            services.AddSingleton<CaretFinderService>();
            services.AddSingleton<InputSimulatorService>();
            services.AddSingleton<SuggestionService>();
            services.AddSingleton<LanguageService>();
            services.AddSingleton<SettingsService>();
            services.AddHostedService<MainService>();
        }).ConfigureLogging((_, logging) =>
        {
            logging.AddConsole();
        }).Build();
        await _host.StartAsync(_hostCancellationTokenSource.Token);
    }

    private bool TryLaunchServiceProcess()
    {
        try
        {
            var currentExecutablePath = Process.GetCurrentProcess().MainModule!.FileName;
            if (string.IsNullOrEmpty(currentExecutablePath))
            {
                Debug.WriteLine("Failed to get current executable path.");
                return false;
            }

            var processStartInfo = new ProcessStartInfo
            {
                FileName = currentExecutablePath,
                Arguments = "--service",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            serviceProcess = Process.Start(processStartInfo);
            Thread.Sleep(100);
            if (serviceProcess is null || serviceProcess.HasExited)
            {
                Debug.WriteLine("Failed to start service process.");
                return false;
            }
            Debug.WriteLine($"Service process started with ID: {serviceProcess.Id}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error launching service process: {ex.Message}");
            return false;
        }
    }

    private async Task InitPipeClientAsync()
    {
        const int maxRetries = 5;
        const int initialRetryDelayMs = 1000;
        var retryCount = 0;
        var connected = false;

        while (!connected && retryCount < maxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    Debug.WriteLine($"Retry attempt {retryCount} of {maxRetries} to connect to pipe...");
                    var delayMs = initialRetryDelayMs + retryCount * 1000;
                    await Task.Delay(delayMs);
                }

                _pipeClient = new(".", PIPE_NAME, PipeDirection.InOut, PipeOptions.Asynchronous);
                await _pipeClient.ConnectAsync(1000);
                connected = true;

                Debug.WriteLine($"Successfully connected to pipe{(retryCount > 0 ? $" after {retryCount} retries" : "")}.");

                _pipeStreamReader = new(_pipeClient);

                _pipeListenerCancellationTokenSource = new CancellationTokenSource();
                _ = PipeListenerTaskAsync(_pipeListenerCancellationTokenSource.Token);

                _pipeStreamWriter = new(_pipeClient)
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                retryCount++;

                if (retryCount >= maxRetries)
                {
                    Debug.WriteLine($"Failed to connect to pipe after {maxRetries} attempts. Last error: {ex.Message}");
                }
                else
                {
                    Debug.WriteLine($"Failed to connect to pipe: {ex.Message}. Will retry in a moment...");

                    _pipeClient?.Dispose();
                    _pipeClient = null;
                }
            }
        }
    }

    private async Task PipeListenerTaskAsync(CancellationToken cancellationToken)
    {
        try
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new CaretRectangleJsonConverter());

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await _pipeStreamReader!.ReadLineAsync(cancellationToken);
                if (line != null)
                {
                    var ipcMessage = JsonSerializer.Deserialize<BaseIpcMessage>(line);

                    if (ipcMessage == null)
                    {
                        continue;
                    }

                    switch (ipcMessage.Type)
                    {
                        case IpcMessageType.StatusMessage:
                            var statusMessage = JsonSerializer.Deserialize<StatusMessage>(line);
                            MainWindow?.HandleStatusMessage(statusMessage!);
                            break;
                        case IpcMessageType.SuggestionMessage:
                            var suggestionMessage = JsonSerializer.Deserialize<SuggestionMessage>(line, options);
                            SuggestionsWindow?.HandleSuggestionMessage(suggestionMessage!);
                            break;
                        case IpcMessageType.CloseMessage:
                            var closeMessage = JsonSerializer.Deserialize<CloseMessage>(line);
                            if (closeMessage is not null)
                            {
                                if (SuggestionsWindow != null)
                                {
                                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(SuggestionsWindow);
                                    PInvoke.ShowWindow((HWND)hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                                }
                            }
                            break;
                        case IpcMessageType.SettingsMessage:
                            var settingsMessage = JsonSerializer.Deserialize<SettingsMessage>(line);
                            if (settingsMessage is not null)
                            {
                                MainWindow?.HandleSettingsMessage(settingsMessage);
                            }
                            break;
                        default:
                            Debug.WriteLine($"Unknown message: {ipcMessage}");
                            break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task SendSelectionMessageAsync(int placement)
    {
        while (_pipeClient is null || _pipeStreamWriter is null || !_pipeClient.IsConnected)
        {
            await Task.Delay(1000);
            Debug.WriteLine("Pipe client is not connected. Retrying...");
        }

        try
        {
            var json = JsonSerializer.Serialize(new SelectionMessage(placement, null));
            await _pipeStreamWriter.WriteLineAsync(json);
            Debug.WriteLine($"Sent selection message to service: {placement}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task SendSelectionMessageAsync(string word)
    {
        while (_pipeClient is null || _pipeStreamWriter is null || !_pipeClient.IsConnected)
        {
            await Task.Delay(1000);
            Debug.WriteLine("Pipe client is not connected. Retrying...");
        }

        try
        {
            var json = JsonSerializer.Serialize(new SelectionMessage(null, word));
            await _pipeStreamWriter.WriteLineAsync(json);
            Debug.WriteLine($"Sent selection message to service: {word}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task RequestSettingsAsync(bool doReset = false)
    {
        while (_pipeClient is null || _pipeStreamWriter is null || !_pipeClient.IsConnected)
        {
            await Task.Delay(1000);
            Debug.WriteLine("Pipe client is not connected. Retrying...");
        }

        try
        {
            var json = JsonSerializer.Serialize(new SettingsRequestMessage(doReset));
            await _pipeStreamWriter.WriteLineAsync(json);
            Debug.WriteLine("Sent settings request to server");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    public async Task SendSettingsMessageAsync(AppSettings settings)
    {
        while (_pipeClient is null || _pipeStreamWriter is null || !_pipeClient.IsConnected)
        {
            await Task.Delay(1000);
            Debug.WriteLine("Pipe client is not connected. Retrying...");
        }

        try
        {
            var json = JsonSerializer.Serialize(new SettingsMessage(settings));
            await _pipeStreamWriter.WriteLineAsync(json);
            Debug.WriteLine("Sent settings to service");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private void CreateTaskBarIcon()
    {
        InitializeWindowHandle();
            
        var iconData = new NOTIFYICONDATAW()
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
            hWnd = (HWND)_windowHandle,
            uID = 0,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_TIP | NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP | NOTIFY_ICON_DATA_FLAGS.NIF_GUID,
            uCallbackMessage = CALLBACK_MESSAGE_ID,
            guidItem = guid,
            szInfoTitle = "QuickType",
            dwInfoFlags = NOTIFY_ICON_INFOTIP_FLAGS.NIIF_INFO | NOTIFY_ICON_INFOTIP_FLAGS.NIIF_RESPECT_QUIET_TIME
        };

        var tip = "QuickType";
        var tipSpan = iconData.szTip.AsSpan();
        tip.AsSpan().CopyTo(tipSpan);
        tipSpan[tip.Length] = '\0';

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "QuickType.Assets.AppIcon.ico";
        using var iconStream = assembly.GetManifestResourceStream(resourceName);

        var icon = new System.Drawing.Icon(iconStream!);
        iconData.hIcon = new HICON(icon.Handle);

        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, iconData);
    }

    private unsafe void InitializeWindowHandle()
    {
        _wndProcDelegate = WndProc;

        var taskbarClassName = stackalloc char[TASKBAR_CLASS_NAME.Length];

        for (var i = 0; i < TASKBAR_CLASS_NAME.Length; i++)
        {
            taskbarClassName[i] = TASKBAR_CLASS_NAME[i];
        }

        var mainModuleName = Process.GetCurrentProcess().MainModule!.ModuleName;

        var windowClass = new WNDCLASSW
        {
            lpfnWndProc = _wndProcDelegate,
            hInstance = new HINSTANCE(PInvoke.GetModuleHandle(mainModuleName).DangerousGetHandle()),
            lpszClassName = new(taskbarClassName)
        };

        PInvoke.RegisterClass(windowClass);

        SafeHandle? nullSafeHandle = null;

        _windowHandle = PInvoke.CreateWindowEx(
            WINDOW_EX_STYLE.WS_EX_APPWINDOW,
            "QuickTypeTaskbarClass",
            "QuickTypeTaskbarWindow",
            WINDOW_STYLE.WS_OVERLAPPED,
            0, 0, 0, 0,
            HWND.Null,
            nullSafeHandle,
            PInvoke.GetModuleHandle(mainModuleName),
            null);
    }

    private LRESULT WndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        if (msg == CALLBACK_MESSAGE_ID)
        {
            switch ((uint)lParam.Value)
            {
                case PInvoke.WM_LBUTTONUP:
                    MainWindow ??= new();
                    MainWindow!.Activate();
                    return new LRESULT(0);
                case PInvoke.WM_RBUTTONUP:
                    ShowContextMenu();
                    return new LRESULT(0);
            }
        }

        return PInvoke.DefWindowProc(hwnd, msg, wParam, lParam);
    }

    private unsafe void ShowContextMenu()
    {
        Current!.MainWindow ??= new();

        var hMenu = PInvoke.CreatePopupMenu();
        SafeHandle safeHMenu = new DestroyMenuSafeHandle(hMenu, false);

        var hInternalMenu = PInvoke.CreatePopupMenu();
        SafeHandle safeHInternalMenu = new DestroyMenuSafeHandle(hInternalMenu, false);
        uint internalIdBase = 2000;
        uint internalIndex = 0;
        foreach (var lang in MainWindow?.Settings.LoadedInternalLanguages ?? [])
        {
            var isChecked = true;
            PInvoke.AppendMenu(safeHInternalMenu, MENU_ITEM_FLAGS.MF_STRING | (isChecked ? MENU_ITEM_FLAGS.MF_CHECKED : 0), internalIdBase + internalIndex, lang.Name == nameof(Hungarian) ? "Magyar" : "Angol");
            internalIndex++;
        }
        foreach (var lang in new[] { nameof(Hungarian), nameof(English) })
        {
            if (!(Current.MainWindow!.Settings.LoadedInternalLanguages.Any(l => l.Name == lang)))
            {
                var isChecked = false;
                PInvoke.AppendMenu(safeHInternalMenu, MENU_ITEM_FLAGS.MF_STRING | (isChecked ? MENU_ITEM_FLAGS.MF_CHECKED : 0), internalIdBase + internalIndex, lang == nameof(Hungarian) ? "Magyar" : "Angol");
                internalIndex++;
            }
        }
        PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_POPUP, new(hInternalMenu.Value), "Beépített nyelvek");

        if (Current.MainWindow!.Settings.CustomLanguages.Count == 0)
        {
            PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_STRING | MENU_ITEM_FLAGS.MF_DISABLED, 0, "Nincsenek egyéni nyelvek");
        }
        else
        {
            var hCustomMenu = PInvoke.CreatePopupMenu();
            SafeHandle safeHCustomMenu = new DestroyMenuSafeHandle(hCustomMenu, false);
            uint customIdBase = 3000;
            uint customIndex = 0;
            foreach (var lang in Current.MainWindow!.Settings.CustomLanguages)
            {
                var isChecked = lang.IsLoaded;
                PInvoke.AppendMenu(safeHCustomMenu, MENU_ITEM_FLAGS.MF_STRING | (isChecked ? MENU_ITEM_FLAGS.MF_CHECKED : 0), customIdBase + customIndex, lang.Name);
                customIndex++;
            }

            PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_POPUP, new(hCustomMenu.Value), "Egyéni nyelvek");
        }

        PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
        PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_STRING, ID_OPEN_SETTINGS, "Beállítások");
        PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, 0, null);
        PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_STRING, ID_EXIT, "Kilépés");

        PInvoke.GetCursorPos(out var pt);
        PInvoke.SetForegroundWindow(new HWND(_windowHandle));

        var selected = (uint)PInvoke.TrackPopupMenu(
            hMenu,
            TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD,
            pt.X, pt.Y, 0, new HWND(_windowHandle), null).Value;

        PInvoke.PostMessage(new HWND(_windowHandle), PInvoke.WM_NULL, new WPARAM(0), new LPARAM(0));
        PInvoke.DestroyMenu(hMenu);

        switch (selected)
        {
            case >= 2000 and < 3000:
            {
                var idx = selected - 2000;
                var allInternal = new List<string>();
                allInternal.AddRange(MainWindow?.Settings.LoadedInternalLanguages.Select(l => l.Name) ?? []);
                allInternal.AddRange(new[] { nameof(Hungarian), nameof(English) }.Where(lang => !(MainWindow?.Settings.LoadedInternalLanguages.Any(l => l.Name == lang) ?? false)));
                if (idx < allInternal.Count)
                {
                    var langName = allInternal[(int)idx];
                    var loaded = MainWindow?.Settings.LoadedInternalLanguages.Any(l => l.Name == langName) ?? false;
                    if (loaded)
                    {
                        var lang = MainWindow?.Settings.LoadedInternalLanguages.FirstOrDefault(l => l.Name == langName);
                        if (lang != null)
                        {
                            Current.MainWindow!.Settings.LoadedInternalLanguages.Remove(lang);
                            Current.MainWindow.UpdateAllLanguageLists();
                            _ = Current.MainWindow.SaveSettings();
                        }
                    }
                    else
                    {
                        var newLang = new InternalLanguageDefinition(langName, Current.MainWindow!.GetNearestPriority());
                        Current.MainWindow.Settings.LoadedInternalLanguages.Add(newLang);
                        Current.MainWindow.UpdateAllLanguageLists();
                        _ = Current.MainWindow.SaveSettings();
                    }
                }
                return;
            }
            case >= 3000 and < 4000:
            {
                var idx = selected - 3000;
                var customLangs = MainWindow?.Settings.CustomLanguages ?? [];
                if (idx < customLangs.Count)
                {
                    var lang = customLangs[(int)idx];
                    if (lang.IsLoaded)
                    {
                        lang.IsLoaded = false;
                    }
                    else
                    {
                        lang.IsLoaded = true;
                        lang.Priority = Current.MainWindow!.GetNearestPriority();
                    }
                    Current.MainWindow!.UpdateAllLanguageLists();
                    _ = Current.MainWindow!.SaveSettings();
                }
                return;
            }
            case ID_OPEN_SETTINGS:
                Current.MainWindow!.Activate();
                break;
            case ID_EXIT:
                _ = Current.Exit();
                break;
        }
    }

    private async new Task Exit()
    {
        try
        {
            if (_pipeClient is { IsConnected: true })
            {
                await SendServiceShutdownAsync();
            }

            if (serviceProcess is { HasExited: false })
            {
                serviceProcess.Kill();
            }

            if (_host != null)
            {
                await _hostCancellationTokenSource.CancelAsync();

                await _host.StopAsync(TimeSpan.FromSeconds(5));
            }

            await _pipeListenerCancellationTokenSource?.CancelAsync()!;
            _pipeStreamWriter?.Dispose();
            _pipeStreamReader?.Dispose();
            _pipeClient?.Dispose();
            _hostCancellationTokenSource.Dispose();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error during shutdown: {ex.Message}");
        }
        finally
        {
            base.Exit();
        }
    }


    public async Task SendRecreateLanguageDatabaseAsync(CustomLanguageDefinition language)
    {
        while (_pipeClient is null || _pipeStreamWriter is null || !_pipeClient.IsConnected)
        {
            await Task.Delay(1000);
            Debug.WriteLine("Pipe client is not connected. Retrying...");
        }

        try
        {
            var json = JsonSerializer.Serialize(new RecreateLanguageDatabaseMessage(language));
            await _pipeStreamWriter.WriteLineAsync(json);
            Debug.WriteLine("Send database recreation message to server...");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }

    private async Task SendServiceShutdownAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(new ServiceShutdownMessage());
            await _pipeStreamWriter!.WriteLineAsync(json);
            Debug.WriteLine("Send shutdown message to service...");
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
        }
    }
}