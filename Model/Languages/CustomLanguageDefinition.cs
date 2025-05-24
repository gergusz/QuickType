using System.Collections.Generic;

namespace QuickType.Model.Languages;
public class CustomLanguageDefinition
{
    public string Name { get; init; }
    public int Priority { get; set; }
    public bool HasAccents { get; init; }
    public Dictionary<char, List<char>>? AccentDict { get; init; }
    public bool UsesHybridTrie { get; init; }
    public int? FrequencyThreshhold { get; init; }
    public string FilePath { get; init; }
    public string ReadString { get; init; }
    public bool IsLoaded { get; set; }

    public CustomLanguageDefinition(
        string name,
        int priority,
        bool hasAccents,
        Dictionary<char, List<char>>? accentDict,
        bool usesHybridTrie,
        int? frequencyThreshhold,
        string filePath,
        string readString,
        bool isLoaded)
    {
        Name = name;
        Priority = priority;
        HasAccents = hasAccents;
        AccentDict = accentDict;
        UsesHybridTrie = usesHybridTrie;
        FrequencyThreshhold = frequencyThreshhold;
        FilePath = filePath;
        ReadString = readString;
        IsLoaded = isLoaded;
    }
}
