using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Globalization.DateTimeFormatting;
using QuickType.Model.Languages;

namespace QuickType.Services
{
    public class LanguageService
    {
        internal List<BaseLanguage> LoadedLanguages
        {
            get;
        } = [];

        public void ChangeLanguagePriorities(params List<(int priority, string languageName)> newPriorityList)
        {
            foreach (var newPriority in newPriorityList)
            {
                if (LoadedLanguages.All(x => x.Name != newPriority.languageName))
                {
                    continue;   
                }
                var language = LoadedLanguages.First(x => x.Name == newPriority.languageName);
                if (language.Priority != newPriority.priority)
                {
                    language.Priority = newPriority.priority;
                }
            }
        }

        public async Task LoadLanguagesAsyncTask(List<InternalLanguageDefinition> loadedInternalLanguages, List<CustomLanguageDefinition> customLanguageDefinitions)
        {
            if (loadedInternalLanguages.Count == 0 && customLanguageDefinitions.Count == 0)
            {
                return;
            }

            List<Task> loadingTasks = [];

            List<BaseLanguage> languagesToBeLoaded = [];

            foreach (var language in loadedInternalLanguages)
            {
                switch (language.Name)
                {
                    case nameof(Hungarian):
                        languagesToBeLoaded.Add(new Hungarian(language.Priority));
                        break;
                    case nameof(English):
                        languagesToBeLoaded.Add(new English(language.Priority));
                        break;
                }
            }

            languagesToBeLoaded.AddRange(from customLanguage in customLanguageDefinitions 
                where customLanguage.IsLoaded 
                select new CustomLanguage(customLanguage.Name, 
                    customLanguage.Priority, customLanguage.HasAccents, 
                    customLanguage.AccentDict, customLanguage.UsesHybridTrie, 
                    customLanguage.FrequencyThreshhold, customLanguage.FilePath, 
                    customLanguage.ReadString));

            foreach (var language in languagesToBeLoaded)
            {
                if (language is CustomLanguage customLanguage)
                {
                    if (customLanguage.UsesHybridTrie)
                    {
                        loadingTasks.Add(new(() =>
                        {
                            customLanguage.LoadHybridTrie();
                            LoadedLanguages.Add(customLanguage);
                        }));
                    }
                    else
                    {
                        loadingTasks.Add(new(() =>
                        {
                            customLanguage.LoadMemoryTrie();
                            LoadedLanguages.Add(customLanguage);
                        }));
                    }
                }
                else
                {
                    loadingTasks.Add(new(() =>
                    {
                        language.LoadHybridTrie();
                        LoadedLanguages.Add(language);
                    }));
                }
            }

            foreach (var task in loadingTasks)
            {
                task.Start();
            }

            await Task.WhenAll(loadingTasks);
        }

        public async Task UnloadLanguagesAsyncTask(List<InternalLanguageDefinition> loadedInternalLanguages,
            List<CustomLanguageDefinition> customLanguageDefinitions)
        {
            if (loadedInternalLanguages.Count == 0 && customLanguageDefinitions.Count == 0)
            {
                return;
            }

            var languagesToRemove = LoadedLanguages.Where(x =>
                loadedInternalLanguages.Any(y => x.Name == y.Name) ||
                customLanguageDefinitions.Any(y => x.Name == y.Name)).ToList();

            foreach (var language in languagesToRemove)
            {
                language.DisposeTrie();
                LoadedLanguages.Remove(language);
            }

            await Task.CompletedTask;
        }

        public async Task RecreateDatabaseOfLanguage(CustomLanguageDefinition customLanguageDefinition)
        {
            if (LoadedLanguages.All(x => x.Name != customLanguageDefinition.Name))
            {
                return;
            }
            var language = LoadedLanguages.First(x => x.Name == customLanguageDefinition.Name);
            if (language is CustomLanguage customLanguage)
            {
                customLanguage.ForceRecreateDatabase();
            }
            await Task.CompletedTask;
        }

        public async Task DeleteLanguagesAsyncTask(List<CustomLanguageDefinition> languagesToDelete)
        {
            foreach (var languageDefinition in languagesToDelete)
            {
                if (LoadedLanguages.All(x => x.Name == languageDefinition.Name))
                {
                    continue;
                }
                var language = LoadedLanguages.First(x => x.Name == languageDefinition.Name);
                if (language is CustomLanguage customLanguage)
                {
                    customLanguage.DeleteLanguage();
                    LoadedLanguages.Remove(language);
                }
                else
                {
                    throw new InvalidOperationException($"Language {languageDefinition.Name} is not a CustomLanguage.");
                }
            }
        }
    }
}
