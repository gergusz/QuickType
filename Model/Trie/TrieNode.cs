using System.Collections.Generic;

namespace QuickType.Model.Trie;

public class TrieNode
{
    private readonly Dictionary<char, TrieNode> _children = new();
    public IReadOnlyDictionary<char, TrieNode> Children => _children;

    public bool IsEndOfWord { get; set; }
    public int Frequency { get; set; }


    public TrieNode? GetChild(char letter)
    {
        return _children.GetValueOrDefault(letter);
    }

    public TrieNode AddChild(char letter)
    {
        if (_children.TryGetValue(letter, out var existingNode))
        {
            return existingNode;
        }

        var newNode = new TrieNode();
        _children[letter] = newNode;
        return newNode;
    }

    public void Clear()
    {
        _children.Clear();
    }
}