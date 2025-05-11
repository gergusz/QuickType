using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        public async Task LoadLanguagesAsyncTask(List<string> loadedInternalLanguages, List<CustomLanguageDefinition> customLanguageDefinitions)
        {
            List<Task> loadingTasks = [];

            List<BaseLanguage> languagesToBeLoaded = [];

            foreach (var language in loadedInternalLanguages)
            {
                switch (language)
                {
                    case nameof(Hungarian):
                        languagesToBeLoaded.Add(new Hungarian());
                        break;
                    case nameof(English):
                        languagesToBeLoaded.Add(new English());
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

        public async Task UnloadLanguagesAsyncTask(List<string> loadedInternalLanguages,
            List<CustomLanguageDefinition> customLanguageDefinitions)
        {
            HashSet<string> languagesToKeep = new(loadedInternalLanguages);

            foreach (var customLanguage in customLanguageDefinitions.Where(cl => cl.IsLoaded))
            {
                languagesToKeep.Add(customLanguage.Name);
            }

            var languagesToUnload = LoadedLanguages.Where(lang => !languagesToKeep.Contains(lang.Name)).ToList();

            foreach (var language in languagesToUnload)
            {
                LoadedLanguages.Remove(language);
            }

            await Task.CompletedTask;
        }
    }
}
