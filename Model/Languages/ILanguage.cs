using QuickType.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.Languages
{
    public interface ILanguage
    {
        public string Name { get; }
        public void LoadFromFile(string path);
        public List<Word> SearchByPrefix(string word);
        public void Insert(string word, int frequency);
    }
}
