using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using QuickType.Model.Trie;
using System.Reflection;

namespace QuickType.Model.Languages
{
    internal partial class Hungarian : ILanguage
    {
        private ITrie _Trie { get; set; }
        public string Name => "Hungarian";

        [GeneratedRegex(@"([a-z]+)|([A-Z]+)|([áéóöőúüűí]+)|([ÁÉÓÖŐÚÜŰÍ]+)")]
        private static partial Regex ValidCharsRegex();

        public Hungarian(string? path = null)
        {
            path ??= $@"{Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData))}\QuickType\{Name}.db";
            _Trie = new HybridTrie($@"Data Source={path}");
        }

        public List<Word> SearchByPrefix(string word, int amount = 5)
        {
            return _Trie.SearchByPrefix(word, amount);
        }

        public void Insert(string word, int frequency)
        {
            _Trie.Insert(word, frequency);
        }

    }
}
