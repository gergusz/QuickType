using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json;
using System.Threading;
using System.Drawing;
using System.Runtime.InteropServices.Marshalling;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using QuickType.Model.Languages;
using QuickType.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Controls;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Win32.SafeHandles;
using QuickType.Model.IPC;
using WinUIEx;
using ABI.Windows.Foundation;
using Microsoft.UI.Dispatching;
using Windows.Win32.System.Com;
using Windows.Win32.Graphics.Gdi;
using QuickType.Model;
using QuickType.WinUI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public MainWindow? MainWindow;
        public SuggestionsWindow? SuggestionsWindow; 
        public static new App Current { get; private set; }

        private const string PIPE_NAME = "QuickTypePipe";
        private NamedPipeClientStream? _pipeClient;
        private StreamReader? _pipeStreamReader;
        private StreamWriter? _pipeStreamWriter;
        private Task? _pipeListenerTask;
        private CancellationTokenSource? _pipeListenerCancellationTokenSource;

        private IHost? _host;
        private readonly CancellationTokenSource _hostCancellationTokenSource = new();

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
            Current = this;
            Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
            this.InitializeComponent();
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            _ = StartHostAsync();
            _ = InitPipeClientAsync();
            CreateTaskBarIcon();

            MainWindow ??= new MainWindow();
            SuggestionsWindow ??= new SuggestionsWindow();
            MainWindow.Activate();

        }

        private HostApplicationBuilder CreateApplicationBuilder()
        {
            var applicationBuilder = Host.CreateApplicationBuilder();
            
            applicationBuilder.Services.AddWindowsService(options =>
            {
                options.ServiceName = "QuickType";
            });

            LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(applicationBuilder.Services);

            applicationBuilder.Services.AddLogging(configure =>
            {
                configure.AddEventLog();
                configure.AddConsole();
            });

            applicationBuilder.Services.AddSingleton<KeyboardCapturer>();
            applicationBuilder.Services.AddSingleton<CaretFinder>();
            applicationBuilder.Services.AddSingleton<InputSimulator>();
            applicationBuilder.Services.AddSingleton<SuggestionService>();
            applicationBuilder.Services.AddSingleton<LanguageService>();
            applicationBuilder.Services.AddSingleton<SettingsService>();
            applicationBuilder.Services.AddHostedService<MainService>();

            return applicationBuilder;
        }

        private async Task StartHostAsync()
        {
            _host = CreateApplicationBuilder().Build();
            await _host.StartAsync(_hostCancellationTokenSource.Token);
        }

        private async Task InitPipeClientAsync()
        {
            const int maxRetries = 20;
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
                    _pipeListenerTask = PipeListenerTaskAsync(_pipeListenerCancellationTokenSource.Token);

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
                            case IpcMessageType.StatusMessage:
                                var statusMessage = JsonSerializer.Deserialize<StatusMessage>(line);
                                MainWindow?.HandleStatusMessage(statusMessage);
                                break;
                            case IpcMessageType.SuggestionMessage:
                                var suggestionMessage = JsonSerializer.Deserialize<SuggestionMessage>(line, options);
                                SuggestionsWindow?.HandleSuggestionMessage(suggestionMessage);
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
                string json = JsonSerializer.Serialize(new SelectionMessage(placement));
                await _pipeStreamWriter.WriteLineAsync(json);
                Debug.WriteLine($"Sent selection message to client: {placement}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public async Task RequestSettingsAsync()
        {
            while (_pipeClient is null || _pipeStreamWriter is null || !_pipeClient.IsConnected)
            {
                await Task.Delay(1000);
                Debug.WriteLine("Pipe client is not connected. Retrying...");
            }

            try
            {
                string json = JsonSerializer.Serialize(new SettingsRequestMessage());
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
                string json = JsonSerializer.Serialize(new SettingsMessage(settings));
                await _pipeStreamWriter.WriteLineAsync(json);
                Debug.WriteLine("Sent settings to server");
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

            var icon = new System.Drawing.Icon(iconStream);
            iconData.hIcon = new HICON(icon.Handle);

            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, iconData);
        }

        private unsafe void InitializeWindowHandle()
        {
            _wndProcDelegate = WndProc;

            var taskbarClassName = stackalloc char[TASKBAR_CLASS_NAME.Length];

            for (int i = 0; i < TASKBAR_CLASS_NAME.Length; i++)
            {
                taskbarClassName[i] = TASKBAR_CLASS_NAME[i];
            }

            string mainModuleName = Process.GetCurrentProcess().MainModule!.ModuleName;

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
            var hMenu = PInvoke.CreatePopupMenu();

            SafeHandle safeHMenu = new DestroyMenuSafeHandle(hMenu, false);

            PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_STRING, ID_OPEN_SETTINGS, "Settings");
            PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_SEPARATOR, (0), null);
            PInvoke.AppendMenu(safeHMenu, MENU_ITEM_FLAGS.MF_STRING, ID_EXIT, "Exit");

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
                case ID_OPEN_SETTINGS:
                    MainWindow ??= new();
                    MainWindow!.Activate();
                    break;
                case ID_EXIT:
                    Current.Exit();
                    break;
            }
        }
    }


}
