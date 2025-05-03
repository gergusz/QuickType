using QuickType.Model.Trie;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Services
{
    public class SuggestionService
    {
        public List<Word> GetSuggestions(LanguageService languageService, string currentBuffer, int maxSuggestionCount)
        {
            return languageService.CurrentLanguage.SearchByPrefix(currentBuffer, maxSuggestionCount);
        }
    }
}
