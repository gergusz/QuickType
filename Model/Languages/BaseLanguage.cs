using QuickType.Model.Trie;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.Languages
{
    internal abstract class BaseLanguage
    {
        protected ITrie? Trie
        {
            get;
            set;
        } = null;
        public string Name { get; init; }
        public bool HasAccents { get; init; }
        public bool IsTrieLoaded => Trie is not null;
        public int Priority { get; set; }

        public Dictionary<char, List<char>>? AccentDict { get; init; }

        public List<Word> SearchByPrefix(string word, bool ignoreAccent, int amount = 5)
        {
            if (!IsTrieLoaded)
            {
                throw new InvalidOperationException("Trie is not loaded.");
            }

            var effectiveAccentDict = HasAccents && ignoreAccent ? AccentDict : null;

            return Trie!.SearchByPrefix(word, ignoreAccent, amount, effectiveAccentDict);
        }

        public void Insert(string word, int frequency)
        {
            if (!IsTrieLoaded)
            {
                throw new InvalidOperationException("Trie is not loaded.");
            }

            Trie!.Insert(word, frequency);
        }

        public void ChangeFrequencyOfHybridTrie(int newFrequency)
        {
            if (!IsTrieLoaded)
            {
                throw new InvalidOperationException("Trie is not loaded.");
            }

            if (Trie is HybridTrie hybridTrie)
            {
                hybridTrie.ChangeFrequency(newFrequency);
            }
            else
            {
                throw new InvalidOperationException("Trie is not a HybridTrie.");
            }
        }

        public void ForceRecreateDatabaseOfHybridTrie()
        {
            if (!IsTrieLoaded)
            {
                throw new InvalidOperationException("Trie is not loaded.");
            }

            if (Trie is HybridTrie hybridTrie)
            {
                hybridTrie.RecreateDatabase();
            }
            else
            {
                throw new InvalidOperationException("Trie is not a HybridTrie.");
            }
        }

        internal void LoadHybridTrie(int frequencyThreshhold = 10, bool forceRecreate = false)
        {
            Trie = new HybridTrie(Name, frequencyThreshhold, forceRecreate);
        }

        
    }
}
