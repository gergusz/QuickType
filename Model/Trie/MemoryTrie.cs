using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

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

        public List<Word> SearchByPrefix(string prefix, bool ignoreAccent, int amount = 5, Dictionary<char, List<char>>? accentDictionary = null)
        {
            var result = new List<Word>();

            if (ignoreAccent && accentDictionary is not null)
            {
                SearchWithAccents(_root, prefix, 0, "", result, accentDictionary);
            }
            else
            {
                var current = _root;

                foreach (var letter in prefix)
                {
                    current = current.GetChild(letter);
                    if (current is null)
                    {
                        return result;
                    }
                }

                Dfs(current, prefix, result);
            }

            return [.. result.OrderByDescending(x => x.frequency).Take(amount)];
        }

        private void SearchWithAccents(TrieNode? node, string prefix, int index, string currentPrefix,
            List<Word> result, Dictionary<char, List<char>> accentDictionary)
        {
            if (node is null) return;
            if (index >= prefix.Length)
            {
                Dfs(node, currentPrefix, result);
                return;
            }

            var letter = prefix[index];

            List<char> charsToTry = [letter];
            if (accentDictionary.TryGetValue(letter, out var value))
            {
                charsToTry.AddRange(value);
            }

            foreach (var c in charsToTry)
            {
                var child = node.GetChild(c);
                if (child is not null)
                {
                    SearchWithAccents(child, prefix, index + 1, currentPrefix + c, result, accentDictionary);
                }
            }
        }

        private static void Dfs(TrieNode node, string currentWord, List<Word> result)
        {
            if (node.IsEndOfWord)
            {
                result.Add((currentWord, node.Frequency));
            }

            foreach (var (key, child) in node.Children)
            {
                Dfs(child, currentWord + key, result);
            }
        }
    }
}
