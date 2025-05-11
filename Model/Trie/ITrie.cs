using System.Collections.Generic;

namespace QuickType.Model.Trie
{
    public interface ITrie
    {
        void Insert(string word, int frequency);
        List<Word> SearchByPrefix(string prefix, bool ignoreAccent, int amount = 5, Dictionary<char, List<char>>? accentDictionary = null);
    }
}