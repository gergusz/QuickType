using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QuickType.Model.Languages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace QuickType.WinUI;

/// <summary>
/// Window for creating custom language definitions
/// </summary>
public sealed partial class CreateCustomLanguage
{
    private string? _selectedFilePath;
    public CustomLanguageDefinition? CreatedLanguage { get; private set; }

    public CreateCustomLanguage()
    {
        this.InitializeComponent();

        ExtendsContentIntoTitleBar = true;

        HasAccentsToggle.IsOn = false;
        UsesHybridTrieToggle.IsOn = true;
        FrequencyThresholdNumberBox.Value = 10;

        HasAccentsToggle.Toggled += (_, _) =>
        {
            AccentDictionaryPanel.Visibility = HasAccentsToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            AccentDictionaryTextBlock.Visibility = HasAccentsToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        };

        AccentDictionaryPanel.Visibility = Visibility.Collapsed;
        AccentDictionaryTextBlock.Visibility = Visibility.Collapsed;

        UsesHybridTrieToggle.Toggled += (_, _) =>
        {
            FrequencyThresholdNumberBox.Visibility = UsesHybridTrieToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            FrequencyThresholdTextBlock.Visibility = UsesHybridTrieToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
        };

        FrequencyThresholdNumberBox.Visibility = Visibility.Visible;
        FrequencyThresholdTextBlock.Visibility = Visibility.Visible;
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var filePicker = new FileOpenPicker();

        WinRT.Interop.InitializeWithWindow.Initialize(filePicker, WinRT.Interop.WindowNative.GetWindowHandle(this));

        filePicker.ViewMode = PickerViewMode.List;
        filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        filePicker.FileTypeFilter.Add(".txt");
        filePicker.FileTypeFilter.Add(".csv");
        filePicker.FileTypeFilter.Add("*");
            

        var file = await filePicker.PickSingleFileAsync();
        if (file != null)
        {
            _selectedFilePath = file.Path;
            FilePathTextBox.Text = file.Path;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (ValidateInputs())
        {
            try
            {
                Dictionary<char, List<char>>? accentDict = null;
                if (HasAccentsToggle.IsOn && !string.IsNullOrWhiteSpace(AccentDictionaryTextBox.Text))
                {
                    accentDict = ParseAccentDictionary(AccentDictionaryTextBox.Text);
                }

                CreatedLanguage = new CustomLanguageDefinition(
                    NameTextBox.Text.Trim(),
                    0,
                    HasAccentsToggle.IsOn,
                    accentDict,
                    UsesHybridTrieToggle.IsOn,
                    UsesHybridTrieToggle.IsOn ? (int)FrequencyThresholdNumberBox.Value : null,
                    _selectedFilePath ?? throw new InvalidOperationException("Selected file path cannot be null when creating language definition!"),
                    ReadStringTextBox.Text.Trim(),
                    false);

                this.Close();
            }
            catch (Exception ex)
            {
                await ShowErrorAsync($"Hiba történt a nyelv létrehozásakor: {ex.Message}");
            }
        }
    }

    private Dictionary<char, List<char>> ParseAccentDictionary(string accentDictText)
    {
        var result = new Dictionary<char, List<char>>();

        var entries = accentDictText.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var parts = entry.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || parts[0].Length != 1)
            {
                throw new FormatException($"Érvénytelen ékezet formátum: {entry}. A helyes formátum: 'a:áà'");
            }

            var baseLetter = parts[0][0];
            var accentedLetters = parts[1].ToCharArray().ToList();

            result.Add(baseLetter, accentedLetters);
        }

        return result;
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            ShowError("A nyelv neve nem lehet üres.");
            return false;
        }

        var name = NameTextBox.Text.Trim();
        if (name.Equals("Hungarian", StringComparison.OrdinalIgnoreCase) || name.Equals("English", StringComparison.OrdinalIgnoreCase))
        {
            ShowError("A nyelv neve nem lehet 'Hungarian' vagy 'English'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_selectedFilePath))
        {
            ShowError("A fájl útvonalát ki kell választani.");
            return false;
        }

        if (!File.Exists(_selectedFilePath))
        {
            ShowError("A megadott fájl nem létezik.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(ReadStringTextBox.Text))
        {
            ShowError("Az olvasási formátumot meg kell adni.");
            return false;
        }

        if (!ReadStringTextBox.Text.Contains("{0}") || !ReadStringTextBox.Text.Contains("{1}"))
        {
            ShowError("Az olvasási formátumnak tartalmaznia kell {0} és {1} helyőrzőket.");
            return false;
        }

        return true;
    }

    private void ShowError(string message)
    {
        _ = ShowErrorAsync(message);
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Hiba",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }
}