using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Globalization.DateTimeFormatting;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickType.Model;
using QuickType.Model.IPC;
using QuickType.Model.Languages;
using WinUIEx;
using System.Xml.Linq;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType.WinUI;
/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : WindowEx
{
    public AppSettings Settings { get; private set; } = new();

    private ObservableCollection<LanguageViewModel> LoadedLanguageItems { get; } = [];
    private ObservableCollection<LanguageViewModel> UnloadedInternalLanguageItems { get; } = [];
    private ObservableCollection<LanguageViewModel> UnloadedCustomLanguageItems { get; } = [];

    public MainWindow()
    {
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBorder);

        Closed += MainWindow_Closed;

        LoadingOverlay.Visibility = Visibility.Visible;

        LoadedLanguagesExpander.ItemsSource = LoadedLanguageItems;
        UnloadedInternalLanguagesExpander.ItemsSource = UnloadedInternalLanguageItems;
        UnloadedCustomLanguagesExpander.ItemsSource = UnloadedCustomLanguageItems;

        Task.Run(() => App.Current.RequestSettingsAsync());
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        App.Current.MainWindow = null;
    }

    public void HandleStatusMessage(StatusMessage statusMessage)
    {
        BackgroundServiceStatus.Description = statusMessage.Status;
    }

    public void HandleSettingsMessage(SettingsMessage settingsMessage)
    {
        Settings = settingsMessage.Settings;

        Settings.PropertyChanged += Settings_PropertyChanged;

        DispatcherQueue.TryEnqueue(() =>
        {
            this.Bindings.Update();
            UpdateAllLanguageLists();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        });
    }

    private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        _ = SaveSettings();

        if (e.PropertyName is nameof(Settings.LoadedInternalLanguages) or nameof(Settings.CustomLanguages))
        {
            DispatcherQueue.TryEnqueue(UpdateAllLanguageLists);
        }
    }

    public async Task SaveSettings()
    {
        await App.Current.SendSettingsMessageAsync(Settings);
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
            {
                Settings.CustomLanguages = new List<CustomLanguageDefinition>();
            }

            Settings.CustomLanguages.Add(createdLanguage);
            UpdateAllLanguageLists();
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

    public void UpdateAllLanguageLists()
    {
        UpdateLoadedLanguagesList();
        UpdateUnloadedInternalLanguagesList();
        UpdateUnloadedCustomLanguagesList();
    }

    private void UpdateLoadedLanguagesList()
    {
        LoadedLanguageItems.Clear();
        LoadedLanguagesExpander.Description = "";
        LoadedLanguagesExpander.IsEnabled = true;

        if (Settings.LoadedInternalLanguages.Count == 0 && (Settings.CustomLanguages.Count == 0 ||
                                                            Settings.CustomLanguages.TrueForAll(x => !x.IsLoaded)))
        {
            LoadedLanguagesExpander.IsEnabled = false;
            LoadedLanguagesExpander.IsExpanded = false;
            LoadedLanguagesExpander.Description = "Nincsenek betöltött nyelvek!";
            return;
        }

        var viewModelList = new List<LanguageViewModel>();

        foreach (var internalLanguage in Settings.LoadedInternalLanguages)
        {
            viewModelList.Add(new()
            {
                Name = internalLanguage.Name == nameof(Hungarian) ? "Magyar" : "Angol",
                Priority = internalLanguage.Priority,
                Description = "Beépített nyelv",
                InternalName = internalLanguage.Name,
                IsCustom = false
            });
        }

        foreach (var customLanguage in Settings.CustomLanguages.Where(x => x.IsLoaded))
        {
            viewModelList.Add(new()
            {
                Name = customLanguage.Name,
                Priority = customLanguage.Priority,
                Description = $"Eredetileg betöltve innen: {customLanguage.FilePath}",
            });
        }

        foreach (var item in viewModelList.OrderByDescending(x => x.Priority))
        {
            LoadedLanguageItems.Add(item);
        }
    }

    private void UpdateUnloadedInternalLanguagesList()
    {
        UnloadedInternalLanguageItems.Clear();
        UnloadedInternalLanguagesExpander.Description = "";
        UnloadedInternalLanguagesExpander.IsEnabled = true;

        if (Settings.LoadedInternalLanguages.Count == 2) //Hungarian & English
        {
            UnloadedInternalLanguagesExpander.IsEnabled = false;
            UnloadedInternalLanguagesExpander.IsExpanded = false;
            UnloadedInternalLanguagesExpander.Description = "Nincsen elérhető, be nem töltött beépített nyelv!";
            return;
        }

        if (Settings.LoadedInternalLanguages.All(x => x.Name != nameof(Hungarian)))
        {
            UnloadedInternalLanguageItems.Add(new()
            {
                Name = "Magyar",
                Description = "Beépített nyelv",
                InternalName = nameof(Hungarian),
                IsCustom = false
            });
        }

        if (Settings.LoadedInternalLanguages.All(x => x.Name != nameof(English)))
        {
            UnloadedInternalLanguageItems.Add(new()
            {
                Name = "Angol",
                Description = "Beépített nyelv",
                InternalName = nameof(English),
                IsCustom = false
            });
        }

    }

    private void UpdateUnloadedCustomLanguagesList()
    {
        UnloadedCustomLanguageItems.Clear();
        UnloadedCustomLanguagesExpander.Description = "";
        UnloadedCustomLanguagesExpander.IsEnabled = true;

        var unloadedCustomLanguages = Settings.CustomLanguages.Where(x => !x.IsLoaded).ToList();

        if (unloadedCustomLanguages.Count == 0)
        {
            UnloadedCustomLanguagesExpander.IsEnabled = false;
            UnloadedCustomLanguagesExpander.IsExpanded = false;
            UnloadedCustomLanguagesExpander.Description = "Nincsen elérhető, be nem töltött egyéni nyelv!";
            return;
        }

        foreach (var customLanguage in unloadedCustomLanguages)
        {
            UnloadedCustomLanguageItems.Add(new()
            {
                Name = customLanguage.Name,
                Description = $"Eredetileg betöltve innen: {customLanguage.FilePath}",
                InternalName = customLanguage.Name,
            });
        }
    }

    private void UnloadLanguage_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null)
        {
            var languageName = button.Tag as string;
            if (!string.IsNullOrEmpty(languageName))
            {
                var internalLanguage = Settings.LoadedInternalLanguages.FirstOrDefault(l => l.Name == languageName);
                if (internalLanguage != null)
                {
                    Settings.LoadedInternalLanguages.Remove(internalLanguage);
                    UpdateAllLanguageLists();
                    _ = SaveSettings();
                    return;
                }

                var customLanguage = Settings.CustomLanguages.FirstOrDefault(l => l.Name == languageName && l.IsLoaded);
                if (customLanguage != null)
                {
                    customLanguage.IsLoaded = false;
                    UpdateAllLanguageLists();
                    _ = SaveSettings();
                }
            }
        }
    }

    private void LoadUnloadedCustomLanguage_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null)
        {
            var languageName = button.Tag as string;
            if (!string.IsNullOrEmpty(languageName))
            {
                var customLanguage = Settings.CustomLanguages.FirstOrDefault(l => l.Name == languageName && !l.IsLoaded);
                if (customLanguage != null)
                {
                    customLanguage.IsLoaded = true;
                    customLanguage.Priority = GetNearestPriority();

                    UpdateAllLanguageLists();
                    _ = SaveSettings();
                }
            }
        }
    }

    private void LoadInternalLanguage_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null)
        {
            var languageName = button.Tag as string;
            if (!string.IsNullOrEmpty(languageName))
            {
                var internalLanguage = Settings.LoadedInternalLanguages.FirstOrDefault(l => l.Name == languageName);
                if (internalLanguage == null)
                {
                    internalLanguage = new InternalLanguageDefinition(languageName, GetNearestPriority());
                    Settings.LoadedInternalLanguages.Add(internalLanguage);
                    UpdateAllLanguageLists();
                    _ = SaveSettings();
                }
            }
        }
    }

    private void LoadedLanguagePriorityInput_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (sender.Tag is not string languageName)
        {
            return;
        }

        var newPriority = (int)args.NewValue;

        var internalLanguage = Settings.LoadedInternalLanguages.FirstOrDefault(l => l.Name == languageName);
        if (internalLanguage != null)
        {
            if (internalLanguage.Priority == newPriority)
            {
                return;
            }

            internalLanguage.Priority = newPriority;
        }

        var customLanguage = Settings.CustomLanguages.FirstOrDefault(l => l.Name == languageName && l.IsLoaded);
        if (customLanguage != null)
        {
            if (customLanguage.Priority == newPriority)
            {
                return;
            }

            customLanguage.Priority = newPriority;
        }

        UpdateLoadedLanguagesList();

        _ = SaveSettings();
    }

    public int GetNearestPriority()
    {
        if (LoadedLanguageItems.Count == 0)
        {
            return 0;
        }

        var maxPriority = LoadedLanguageItems.Max(x => x.Priority);
        
        return Math.Min(maxPriority + 1, 10000);
    }

    private void ResetSettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        Settings.PropertyChanged -= Settings_PropertyChanged;

        LoadedLanguageItems.Clear();
        UnloadedInternalLanguageItems.Clear();
        UnloadedCustomLanguageItems.Clear();

        Task.Run(() => App.Current.RequestSettingsAsync(true));
    }

    private void ReloadUnloadedCustomLanguage_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null)
        {
            var languageName = button.Tag as string;
            if (!string.IsNullOrEmpty(languageName))
            {
                var customLanguage = Settings.CustomLanguages.FirstOrDefault(l => l.Name == languageName && l.IsLoaded);
                if (customLanguage != null)
                {
                    Task.Run(() => App.Current.SendRecreateLanguageDatabaseAsync(customLanguage));
                }
            }
        }
    }

    private void RemoveUnloadedCustomLanguage_Click(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        if (button != null)
        {
            var languageName = button.Tag as string;
            if (!string.IsNullOrEmpty(languageName))
            {
                var customLanguage = Settings.CustomLanguages.FirstOrDefault(l => l.Name == languageName && !l.IsLoaded);
                if (customLanguage != null)
                {
                    Settings.CustomLanguages.Remove(customLanguage);
                    UpdateAllLanguageLists();
                    _ = SaveSettings();
                }
            }
        }
    }
}
