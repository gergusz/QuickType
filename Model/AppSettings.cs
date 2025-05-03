using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace QuickType.Model;
public partial class AppSettings : INotifyPropertyChanged
{
    //public string? Language
    //{
    //    get; set;
    //}
    //public string? DictionaryPath
    //{
    //    get; set;
    //}
    //public string? UserDictionaryPath
    //{
    //    get; set;
    //}
    //public string? UserDictionaryFileName
    //{
    //    get; set;
    //}
    //public string? UserDictionaryFilePath
    //{
    //    get; set;
    //}
    //public string? UserDictionaryFileFullPath
    //{
    //    get; set;
    //}
    //public bool IsUserDictionaryEnabled { get; set; } = true;
    //public bool IsAutoCorrectEnabled { get; set; } = true;
    //public bool IsAutoCompleteEnabled { get; set; } = true;
    //public bool IsAutoPunctuationEnabled { get; set; } = true;

    [JsonIgnore] private bool _startWithWindows = false;

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

    [JsonIgnore] private List<string> _activeLanguages = [];

    public List<string> ActiveLanguages
    {
        get => _activeLanguages;
        set
        {
            if (value == _activeLanguages)
            {
                return;
            }

            _activeLanguages = value;
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

    //https://learn.microsoft.com/en-us/dotnet/desktop/wpf/data/how-to-implement-property-change-notification
    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
