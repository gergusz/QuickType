using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model
{
    public class Trie
    {
        private readonly TrieNode root;

        public Trie()
        {
            root = new();
        }

        public void Insert(string word, int frequency)
        {
            var current = root;

            foreach (var letter in word)
            {
                if (!current.Children.TryGetValue(letter, out TrieNode? value))
                {
                    value = new();
                    current.Children[letter] = value;
                }
                current = value;
            }

            current.IsEndOfWord = true;
            current.Frequency = frequency;
        }

        public bool Search(string word)
        {
            var current = root;

            foreach (var letter in word)
            {
                if (!current.Children.TryGetValue(letter, out TrieNode? value))
                {
                    return false;
                }
                current = value;
            }

            return current.IsEndOfWord;
        }

        public List<Word> SearchByPrefix(string prefix, int amount = 5)
        {
            var result = new List<Word>();
            var current = root;

            foreach (var letter in prefix)
            {
                if (!current.Children.TryGetValue(letter, out TrieNode? value))
                {
                    return result;
                }
                current = value;
            }

            DFS(current, prefix, result);
            return [.. result.OrderByDescending(x => x.frequency)];
        }

        private static void DFS(TrieNode node, string currentWord, List<Word> result)
        {
            if (node.IsEndOfWord)
            {
                result.Add((currentWord, node.Frequency));
            }

            foreach (var child in node.Children)
            {
                DFS(child.Value, currentWord + child.Key, result);
            }
        }
    }

    public record struct Word(string word, int frequency)
    {
        public static implicit operator (string word, int frequency)(Word value)
        {
            return (value.word, value.frequency);
        }

        public static implicit operator Word((string word, int frequency) value)
        {
            return new Word(value.word, value.frequency);
        }
    }
}
