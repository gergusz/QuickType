using QuickType.Model.Trie;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Languages;

namespace QuickType.Services
{
    public class SuggestionService
    {
        internal List<Word> GetSuggestions(List<BaseLanguage> loadedLanguages, string currentBuffer, bool ignoreAccent, int maxSuggestionCount)
        {
            var suggestions = new List<Word>();
            foreach (var language in loadedLanguages.OrderBy(x => x.Priority))
            {
                if (!language.IsTrieLoaded)
                {
                    continue;
                }

                var languageSuggestions = language.SearchByPrefix(currentBuffer, ignoreAccent, maxSuggestionCount);
                suggestions.AddRange(languageSuggestions);
                if (suggestions.Count >= maxSuggestionCount)
                {
                    break;
                }
            }
            return suggestions.Distinct().ToList();
        }
    }
}
