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
        public ILanguage CurrentLanguage;

        public List<ILanguage> LoadedLanguages;

        public LanguageService()
        {
            CurrentLanguage = new Hungarian();
        }
    }
}
