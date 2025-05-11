using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Trie;

namespace QuickType.Model.Languages;
internal class English : BaseLanguage
{
    internal English()
    {
        Priority = 0;
        Name = nameof(English);
        HasAccents = false;
        AccentDict = null;
    }

}
