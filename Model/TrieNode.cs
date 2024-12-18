using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model
{
    public class TrieNode
    {
        public List<(char Key, TrieNode Node)> Children { get; } = [];
        public bool IsEndOfWord { get; set; }
        public int Frequency { get; set; }
        public TrieNode() { }

        public TrieNode GetChild(char letter)
        {
            return Children.FirstOrDefault(x => x.Key == letter).Node;
        }

        public TrieNode AddChild(char letter)
        {
            var node = GetChild(letter);
            if (node is not null) return node;

            node = new TrieNode();
            Children.Add((letter, node));
            return node;
        }

    }
}
