using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.Languages;
public record CustomLanguageDefinition(string Name, int Priority, bool HasAccents, Dictionary<char, List<char>>? AccentDict, bool UsesHybridTrie, int? FrequencyThreshhold, string FilePath, string ReadString, bool IsLoaded)
{
}
