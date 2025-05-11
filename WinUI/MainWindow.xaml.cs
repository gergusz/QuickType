using System.Collections.Generic;
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

    public bool IsHungarianLoaded => Settings.LoadedInternalLanguages?.Contains("Hungarian") ?? false;
    public bool IsHungarianNotLoaded => !IsHungarianLoaded;
    public bool IsEnglishLoaded => Settings.LoadedInternalLanguages?.Contains("English") ?? false;
    public bool IsEnglishNotLoaded => !IsEnglishLoaded;

    private void LoadHungarianButton_Click(object sender, RoutedEventArgs e)
    {
        if (Settings.LoadedInternalLanguages == null)
            Settings.LoadedInternalLanguages = new List<string>();

        if (!Settings.LoadedInternalLanguages.Contains("Hungarian"))
        {
            Settings.LoadedInternalLanguages.Add("Hungarian");
            this.Bindings.Update();
        }
    }

    private void UnloadHungarianButton_Click(object sender, RoutedEventArgs e)
    {
        if (Settings.LoadedInternalLanguages?.Contains("Hungarian") ?? false)
        {
            Settings.LoadedInternalLanguages.Remove("Hungarian");
            this.Bindings.Update();
        }
    }

    private void LoadEnglishButton_Click(object sender, RoutedEventArgs e)
    {
        if (Settings.LoadedInternalLanguages == null)
            Settings.LoadedInternalLanguages = new List<string>();

        if (!Settings.LoadedInternalLanguages.Contains("English"))
        {
            Settings.LoadedInternalLanguages.Add("English");
            this.Bindings.Update();
        }
    }

    private void UnloadEnglishButton_Click(object sender, RoutedEventArgs e)
    {
        if (Settings.LoadedInternalLanguages?.Contains("English") ?? false)
        {
            Settings.LoadedInternalLanguages.Remove("English");
            this.Bindings.Update();
        }
    }

    private async void AddCustomLanguage_OnClick(object sender, RoutedEventArgs e)
    {
        var createLanguageWindow = new CreateCustomLanguage();

        createLanguageWindow.Activate();

        await WaitForWindowCloseAsync(createLanguageWindow);

        var createdLanguage = createLanguageWindow.CreatedLanguage;

        if (createdLanguage != null)
        {
            if (Settings.CustomLanguages == null)
                Settings.CustomLanguages = new List<Model.Languages.CustomLanguageDefinition>();

            Settings.CustomLanguages.Add(createdLanguage);

            this.Bindings.Update();

            await SaveSettings();
        }
    }

    private Task WaitForWindowCloseAsync(WindowEx window)
    {
        var tcs = new TaskCompletionSource();

        window.Closed += (s, e) =>
        {
            tcs.SetResult();
        };

        return tcs.Task;
    }
}
