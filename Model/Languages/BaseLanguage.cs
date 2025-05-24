using QuickType.Model.Trie;
using System;
using System.Collections.Generic;

namespace QuickType.Model.Languages;

internal abstract class BaseLanguage
{
    protected ITrie? Trie
    {
        get;
        set;
    }
    public string Name { get; init; } = null!;
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

    public void DeleteLanguage()
    {
        if (IsTrieLoaded)
        {
            throw new InvalidOperationException("Trie is loaded, will not delete!");
        }

        if (Trie is HybridTrie hybridTrie)
        {
            hybridTrie.DeleteDatabase();
        }

        DisposeTrie();
    }

    public void LoadHybridTrie(int frequencyThreshhold = 10)
    {
        Trie = new HybridTrie(Name, frequencyThreshhold);
    }

    public void DisposeTrie()
    {
        Trie?.Dispose();
        Trie = null;
    }
        
}