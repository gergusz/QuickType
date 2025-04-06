using QuickType.Model.Trie;
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
        public List<Word> SearchByPrefix(string word, int amount = 5);
        public void Insert(string word, int frequency);
    }
}
