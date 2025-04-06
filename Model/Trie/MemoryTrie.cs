using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.Trie
{
    public class MemoryTrie : ITrie
    {
        private readonly TrieNode _root;

        public MemoryTrie()
        {
            _root = new();
        }

        public void Insert(string word, int frequency)
        {
            var current = _root;

            foreach (var letter in word)
            {
                current = current.AddChild(letter);
            }

            current.IsEndOfWord = true;
            current.Frequency = frequency;
        }

        public bool Search(string word)
        {
            var current = _root;

            foreach (var letter in word)
            {
                current = current.GetChild(letter);
                if (current is null) return false;
            }

            return current.IsEndOfWord;
        }

        public List<Word> SearchByPrefix(string prefix, int amount = 5)
        {
            var result = new List<Word>();
            var current = _root;

            foreach (var letter in prefix)
            {
                current = current.GetChild(letter);
                if (current is null) return result;
            }

            DFS(current, prefix, result);
            return [.. result.OrderByDescending(x => x.frequency).Take(amount)];
        }

        private static void DFS(TrieNode node, string currentWord, List<Word> result)
        {
            if (node.IsEndOfWord)
            {
                result.Add((currentWord, node.Frequency));
            }

            foreach (var (key, child) in node.Children)
            {
                DFS(child, currentWord + key, result);
            }
        }
    }
}
