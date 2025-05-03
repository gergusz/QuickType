using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using QuickType.Model;
using QuickType.Model.IPC;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType.WinUI;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : WindowEx
{
    // Change to public property to allow x:Bind
    public AppSettings Settings { get; private set; } = new();

    public MainWindow()
    {
        this.InitializeComponent();
        Closed += MainWindow_Closed;

        LoadingOverlay.Visibility = Visibility.Visible;

        Task.Run(App.Current.RequestSettingsAsync);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        App.Current.MainWindow = null;
    }

    public void HandleStatusMessage(StatusMessage statusMessage)
    {
        //BackgroundServiceStatus.Description = $"Current status of service: {statusMessage.Status}";
    }

    public void HandleSettingsMessage(SettingsMessage settingsMessage)
    {
        Settings = settingsMessage.Settings;

        Settings.PropertyChanged += Settings_PropertyChanged;

        DispatcherQueue.TryEnqueue(() =>
        {
            // Trigger binding refresh
            this.Bindings.Update();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        });
    }

    private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = SaveSettings();
    }

    private async Task SaveSettings()
    {
        await App.Current.SendSettingsMessageAsync(Settings);
    }
}
