using System.Collections.Generic;

namespace QuickType.Model.Trie
{
    public interface ITrie
    {
        void Insert(string word, int frequency);
        List<Word> SearchByPrefix(string prefix, int amount = 5);
    }
}