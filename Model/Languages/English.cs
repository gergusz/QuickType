using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Trie;

namespace QuickType.Model.Languages;
internal class English : BaseLanguage
{
    internal English(int priority)
    {
        Priority = priority;
        Name = nameof(English);
        HasAccents = false;
        AccentDict = null;
    }

}
