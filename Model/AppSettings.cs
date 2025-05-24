using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using QuickType.Model.Languages;

namespace QuickType.Model;
public sealed partial class AppSettings : INotifyPropertyChanged
{
    [JsonIgnore] private bool _startWithWindows;

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (value == _startWithWindows)
            {
                return;
            }

            _startWithWindows = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] private int _maxSuggestions = 5;

    public int MaxSuggestions
    {
        get => _maxSuggestions;
        set
        {
            if (value == _maxSuggestions)
            {
                return;
            }

            value = value switch
            {
                < 1 => 1,
                > 10 => 10,
                _ => value
            };
            _maxSuggestions = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] private bool _startAtOne = true;

    public bool StartAtOne
    {
        get => _startAtOne;
        set
        {
            if (value == _startAtOne)
            {
                return;
            }

            _startAtOne = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] private bool _ignoreAccent;

    public bool IgnoreAccent
    {
        get => _ignoreAccent;
        set
        {
            if (value == _ignoreAccent)
            {
                return;
            }
            _ignoreAccent = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] private List<CustomLanguageDefinition> _customLanguageDefinitions = [];

    public List<CustomLanguageDefinition> CustomLanguages
    {
        get => _customLanguageDefinitions;
        set
        {
            if (value == _customLanguageDefinitions)
            {
                return;
            }
            _customLanguageDefinitions = value;
            OnPropertyChanged();
        }
    }

    [JsonIgnore] private List<InternalLanguageDefinition> _loadedInternalLanguages = [];

    public List<InternalLanguageDefinition> LoadedInternalLanguages
    {
        get => _loadedInternalLanguages;
        set
        {
            if (value == _loadedInternalLanguages)
            {
                return;
            }

            if (value.Count == 0 || !value.TrueForAll(x => x.Name.Equals(nameof(Hungarian)) || x.Name.Equals(nameof(English))))
            {
                value = [];
            }
            _loadedInternalLanguages = value;
            OnPropertyChanged();
        }
    }

    //https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-implement-property-change-notification
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
