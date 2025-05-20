using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;

namespace QuickType.Model.Trie
{
    public sealed class MemoryTrie : ITrie
    {
        private readonly TrieNode _root = new();

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
                SearchByPrefixWithAccents(_root, prefix, 0, new(), result, accentDictionary);
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

                Dfs(current, new StringBuilder(prefix), result);
            }

            return [.. result.OrderByDescending(x => x.frequency).Take(amount)];
        }

        private void SearchByPrefixWithAccents(TrieNode? node, string prefix, int index, StringBuilder currentPrefix,
            List<Word> result, Dictionary<char, List<char>> accentDictionary)
        {
            if (node is null)
            {
                return;
            }

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
                    currentPrefix.Append(c);
                    SearchByPrefixWithAccents(child, prefix, index + 1, currentPrefix, result, accentDictionary);
                    currentPrefix.Length--;
                }
            }
        }
        private void Dfs(TrieNode node, StringBuilder currentWord, List<Word> result)
        {
            if (node.IsEndOfWord)
            {
                result.Add((currentWord.ToString(), node.Frequency));
            }

            foreach (var pair in node.Children)
            {
                currentWord.Append(pair.Key);
                Dfs(pair.Value, currentWord, result);
                currentWord.Length--;
            }
        }

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                ClearTrieNode(_root);

            }

            _disposed = true;
        }

        private void ClearTrieNode(TrieNode? node)
        {
            if (node == null)
            {
                return;
            }

            foreach (var children in node.Children)
            {
                ClearTrieNode(children.Value);
            }

            node.Clear();
        }

        ~MemoryTrie()
        {
            Dispose(false);
        }
    }
}
