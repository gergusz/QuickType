using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using QuickType.Model.IPC;
using System.Diagnostics;

namespace QuickType.Services
{
    public class SettingsService
    {
        private readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickType", "settings.json");
        private readonly SemaphoreSlim _settingsSemaphore = new(1, 1);

        public AppSettings AppSettings
        {
            get;
            private set;
        } = new();

        public SettingsService()
        {
            Task.Run(LoadSettingsFromFileAsync);
        }

        private void _appSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Task.Run(SaveSettingsToFileAsync);

            if (e.PropertyName == nameof(AppSettings.StartWithWindows))
            {
                RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if (AppSettings.StartWithWindows)
                {
                    regKey!.SetValue(Assembly.GetExecutingAssembly().FullName, Environment.ProcessPath);
                }
                else
                {
                    regKey!.DeleteValue(Assembly.GetExecutingAssembly().FullName);
                }
            }
        }

        private async Task SaveSettingsToFileAsync()
        {
            await _settingsSemaphore.WaitAsync();
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var json = JsonSerializer.Serialize(AppSettings, options);

                var directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var tempPath = _filePath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json);
                File.Move(tempPath, _filePath, true);
            }
            finally
            {
                _settingsSemaphore.Release();
            }
        }

        private async Task LoadSettingsFromFileAsync()
        {
            await _settingsSemaphore.WaitAsync();
            try
            {
                if (!File.Exists(_filePath))
                {
                    _settingsSemaphore.Release();
                    await SaveSettingsToFileAsync();
                    return;
                }

                var fileContent = await File.ReadAllTextAsync(_filePath);
                if (string.IsNullOrWhiteSpace(fileContent))
                {
                    _settingsSemaphore.Release();
                    await SaveSettingsToFileAsync();
                    return;
                }

                var loadedSettings = JsonSerializer.Deserialize<AppSettings>(fileContent);
                if (loadedSettings != null)
                {
                    if (AppSettings != null)
                    {
                        AppSettings.PropertyChanged -= _appSettings_PropertyChanged;
                    }

                    AppSettings = loadedSettings;
                    AppSettings.PropertyChanged += _appSettings_PropertyChanged;
                }
                else
                {
                    AppSettings = new AppSettings();
                    AppSettings.PropertyChanged += _appSettings_PropertyChanged;

                    _settingsSemaphore.Release();
                    await SaveSettingsToFileAsync();
                }
            }
            catch (Exception)
            {
                AppSettings = new AppSettings();
                AppSettings.PropertyChanged += _appSettings_PropertyChanged;

                _settingsSemaphore.Release();
                await SaveSettingsToFileAsync();
            }
            finally
            {
                if (_settingsSemaphore.CurrentCount == 0)
                {
                    _settingsSemaphore.Release();
                }
            }
        }

        public async Task ResetSettingsAsync()
        {
            await _settingsSemaphore.WaitAsync();
            try
            {
                if (AppSettings != null)
                {
                    AppSettings.PropertyChanged -= _appSettings_PropertyChanged;
                }

                AppSettings = new AppSettings();
                AppSettings.PropertyChanged += _appSettings_PropertyChanged;
            }
            finally
            {
                _settingsSemaphore.Release();
            }

            await SaveSettingsToFileAsync();
        }

        public async Task HandleSettingsMessageAsync(AppSettings newSettings)
        {
            await _settingsSemaphore.WaitAsync();
            try
            {
                foreach (var property in typeof(AppSettings).GetProperties())
                {
                    var newValue = property.GetValue(newSettings);
                    if (newValue != null)
                    {
                        property.SetValue(AppSettings, newValue);
                    }
                }
            }
            finally
            {
                _settingsSemaphore.Release();
            }
        }
    }
}
