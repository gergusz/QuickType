using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model
{
    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; } = [];
        public bool IsEndOfWord { get; set; }
        public int Frequency { get; set; } = 0;

        public TrieNode() { }
    }
}
