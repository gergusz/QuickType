using QuickType.Model;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.Languages
{
    internal class Hungarian : ILanguage
    {
        private Trie Trie { get; set; }
        public string Name => "Hungarian";

        public Hungarian(string path)
        {
            LoadFromFile(path);
        }

        public void LoadFromFile(string path)
        {
            Trie = new();
            foreach (var line in File.ReadLines(path))
            {
                if (line == "word,freq") continue;
                var parts = line.Split(',');
                var word = parts[0];
                var freq = int.Parse(parts[1]);
                Trie.Insert(word, freq);
            }
        }

        public List<Word> SearchByPrefix(string word)
        {
            return Trie.SearchByPrefix(word);
        }

        public void Insert(string word, int frequency)
        {
            Trie.Insert(word, frequency);
        }

    }
}
