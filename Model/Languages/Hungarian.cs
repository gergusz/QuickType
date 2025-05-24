using System.Collections.Generic;

namespace QuickType.Model.Languages;

internal class Hungarian : BaseLanguage
{
    internal Hungarian(int priority)
    {
        Priority = priority;
        Name = nameof(Hungarian);
        HasAccents = true;
        AccentDict = new Dictionary<char, List<char>>()
        {
            {'a', ['á'] },
            {'e', ['é'] },
            {'i', ['í'] },
            {'o', ['ó', 'ö', 'ő'] },
            {'u', ['ú', 'ü', 'ű'] }
        };
    }
}