using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Trie;

namespace QuickType.Model.Languages;

internal class CustomLanguage : BaseLanguage
{
    private string _filePath;
    private string _readString;
    private int? _frequencyThreshold;
    public bool UsesHybridTrie;

    internal CustomLanguage(string name, int priority, bool hasAccents, Dictionary<char, List<char>>? accentDict,
        bool usesHybridTrie, int? frequencyThreshold, string filePath, string readString)
    {
        if (name is nameof(Hungarian) or nameof(English))
        {
            throw new ArgumentException($"Name cannot be {nameof(Hungarian)}, or {nameof(English)}.");
        }

        if (usesHybridTrie && frequencyThreshold is null)
        {
            throw new ArgumentException("Frequency threshold must be provided when using HybridTrie.");
        }

        Name = name;
        Priority = priority;
        HasAccents = hasAccents;
        AccentDict = accentDict;
        UsesHybridTrie = usesHybridTrie;
        _frequencyThreshold = frequencyThreshold;
        _filePath = filePath;
        _readString = readString;
    }


    public void LoadHybridTrie()
    {
        Trie = new HybridTrie(Name, _filePath, _readString, _frequencyThreshold ?? 10);
    }

    public void LoadMemoryTrie()
    {
        Trie = new MemoryTrie();

        var readParts = _readString.Split(["{0}", "{1}"], StringSplitOptions.None);

        if (readParts.Length < 3)
        {
            throw new InvalidOperationException(
                $"Invalid read string format: '{_readString}'. Expected format with {{0}} for word and {{1}} for frequency.");
        }

        var prefix = readParts[0];
        var middle = readParts[1];
        var suffix = readParts[2];

        using var reader = new StreamReader(_filePath);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                continue;
            }

            try
            {
                if (!line.StartsWith(prefix) || !line.EndsWith(suffix))
                {
                    Debug.WriteLine($"Line '{line}' does not match pattern '{_readString}'");
                    continue;
                }

                var withoutPrefix = line[prefix.Length..];
                var withoutSuffix = withoutPrefix[..^suffix.Length];
                var parts = withoutSuffix.Split(middle);

                if (parts.Length != 2)
                {
                    Debug.WriteLine(
                        $"Could not extract word and frequency from line '{line}' using pattern '{_readString}'");
                    continue;
                }

                var word = parts[0].Trim();
                var freq = int.Parse(parts[1].Trim());

                Trie!.Insert(word, freq);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing line '{line}': {ex.Message}");
                throw;
            }
        }
    }

    public void ForceRecreateDatabase()
    {
        if (UsesHybridTrie && Trie is HybridTrie hybridTrie)
        {
            hybridTrie.RecreateDatabase();
        }
        else if (Trie is MemoryTrie)
        {
            LoadMemoryTrie();
        }
    }

}
