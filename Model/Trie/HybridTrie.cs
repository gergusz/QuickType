using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Xml.Linq;
using QuickType.Model.Languages;

namespace QuickType.Model.Trie
{
    public class HybridTrie : ITrie
    {
        private MemoryTrie _memoryTrie;
        private int _frequencyThreshhold;
        private readonly string _connectionString;
        private readonly string? _embeddedResourceName;
        private readonly string? _filePath;
        private readonly string? _readString;

        internal HybridTrie(string name, int frequencyThreshold = 10, bool forceRecreate = false)
        {
            _memoryTrie = new MemoryTrie();
            _connectionString =
                $@"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QuickType")}\Languages\{name}.db";
            _frequencyThreshhold = frequencyThreshold;

            if (name == nameof(Hungarian))
            {
                _embeddedResourceName = "QuickType.hu_full.txt";
            } 
            else if (name == nameof(English))
            {
                _embeddedResourceName = "QuickType.en_full.txt";
            }
            else
            {
                _embeddedResourceName = null;
            }

            if (forceRecreate || !Path.Exists($"{_connectionString.Split("=")[1]}"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_connectionString.Split("=")[1])!);
                RecreateDatabase();
            }

            FillMemoryTrie();
        }

        public HybridTrie(string name, string filePath, string readString, int frequencyThreshhold = 10, bool forceRecreate = false) 
            : this (name, frequencyThreshhold, forceRecreate)
        {
            _filePath = filePath;
            _readString = readString;
        }

        public void RecreateDatabase()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();

            var dropCmd = conn.CreateCommand();
            dropCmd.CommandText = @"DROP TABLE IF EXISTS Words";
            dropCmd.ExecuteNonQuery();

            var createCmd = conn.CreateCommand();
            createCmd.CommandText = @"
                CREATE TABLE Words (
                    Word TEXT PRIMARY KEY,
                    Frequency INTEGER
                );
                ";
            createCmd.ExecuteNonQuery();

            using var transaction = conn.BeginTransaction();
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"INSERT INTO Words (Word, Frequency) VALUES ($word, $frequency)";

            var wordParam = insertCmd.CreateParameter();
            wordParam.ParameterName = "$word";
            insertCmd.Parameters.Add(wordParam);

            var freqParam = insertCmd.CreateParameter();
            freqParam.ParameterName = "$frequency";
            insertCmd.Parameters.Add(freqParam);

            if (_embeddedResourceName is not null)
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(_embeddedResourceName) ?? throw new InvalidOperationException($"Embedded resource {_embeddedResourceName} not found.");
                using var reader = new StreamReader(stream);
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line != null)
                    {
                        var parts = line.Split(' ');
                        var word = parts[0];
                        var freq = int.Parse(parts[1]);

                        wordParam.Value = word;
                        freqParam.Value = freq;
                        try
                        {
                            insertCmd.ExecuteNonQuery();
                        }
                        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) //SQLite Error 19: UNIQUE constraint failed
                        {
                            Debug.WriteLine($"Duplicate entry for word '{word}' with frequency {freq}. Skipping this entry.");
                        }
                    }
                }
            }
            else
            {
                if (_filePath is null || _readString is null)
                {
                    throw new InvalidOperationException("File path or read string is not given, but not an embedded language!");
                }

                var readParts = _readString.Split(["{0}", "{1}"], StringSplitOptions.None);

                if (readParts.Length < 3)
                {
                    throw new InvalidOperationException($"Invalid read string format: '{_readString}'. Expected format with {{0}} for word and {{1}} for frequency.");
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
                            Debug.WriteLine($"Could not extract word and frequency from line '{line}' using pattern '{_readString}'");
                            continue;
                        }

                        var word = parts[0].Trim();
                        var freq = int.Parse(parts[1].Trim());

                        wordParam.Value = word;
                        freqParam.Value = freq;

                        try
                        {
                            insertCmd.ExecuteNonQuery();
                        }
                        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) //SQLite Error 19: UNIQUE constraint failed
                        {
                            Debug.WriteLine($"Duplicate entry for word '{word}' with frequency {freq}. Skipping this entry.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error processing line '{line}': {ex.Message}");
                        throw;
                    }
                }

            }

            transaction.Commit();
        }

        public void Insert(string word, int frequency)
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
                    INSERT INTO Words (Word, Frequency)
                    VALUES ($word, $frequency)
                    ";
            insertCmd.Parameters.AddWithValue("$word", word);
            insertCmd.Parameters.AddWithValue("$frequency", frequency);
            insertCmd.ExecuteNonQuery();

            if (frequency > _frequencyThreshhold)
            {
                _memoryTrie.Insert(word, frequency);
            }
        }

        private void FillMemoryTrie()
        {
            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var selectCmd = conn.CreateCommand();
            selectCmd.CommandText = @"
                    SELECT Word, Frequency
                    FROM Words
                    WHERE Frequency > $frequency
                    ";
            selectCmd.Parameters.AddWithValue("$frequency", _frequencyThreshhold);
            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                _memoryTrie.Insert(reader.GetString(0), reader.GetInt32(1));
            }
        }


        public List<Word> SearchByPrefix(string prefix, bool ignoreAccent, int amount = 5, Dictionary<char, List<char>>? accentDictionary = null)
        {
            var result = _memoryTrie.SearchByPrefix(prefix, ignoreAccent, amount, accentDictionary);

            if (result.Count >= amount)
            {
                return result;
            }

            using var conn = new SqliteConnection(_connectionString);
            conn.Open();
            var selectCmd = conn.CreateCommand();

            if (ignoreAccent && accentDictionary is not null)
            {
                var commandText = new StringBuilder(@"
                        SELECT Word, Frequency
                        FROM Words
                        WHERE Word LIKE $prefix || '%'");

                var index = 0;
                var accentedList = new List<(string paramName, string value)>();

                for (var i = 0; i < prefix.Length; i++)
                {
                    var c = prefix[i];
                    if (!accentDictionary.TryGetValue(c, out var charList))
                    {
                        continue;
                    }

                    foreach (var accentedChar in charList)
                    {
                        index++;
                        var paramName = $"$accentedPrefix{index}";
                        var accentedPrefix = $"{prefix.AsSpan(0, i)}{accentedChar}{prefix.AsSpan(i + 1)}";
                        accentedList.Add((paramName, accentedPrefix));
                        commandText.Append($" OR Word LIKE {paramName} || '%'");
                    }
                }

                commandText.Append(@"
                        ORDER BY Frequency DESC
                        LIMIT $amount");

                selectCmd.CommandText = commandText.ToString();
                selectCmd.Parameters.AddWithValue("$prefix", prefix);
                selectCmd.Parameters.AddWithValue("$amount", amount - result.Count);

                foreach (var (paramName, value) in accentedList)
                {
                    selectCmd.Parameters.AddWithValue(paramName, value);
                }
            }
            else
            {
                selectCmd.CommandText = @"
                    SELECT Word, Frequency
                    FROM Words
                    WHERE Word LIKE $prefix || '%'
                    ORDER BY Frequency DESC
                    LIMIT $amount
                    ";
                selectCmd.Parameters.AddWithValue("$prefix", prefix);
                selectCmd.Parameters.AddWithValue("$amount", amount - result.Count);
            }

            using var reader = selectCmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new Word(reader.GetString(0), reader.GetInt32(1)));
            }

            return result;
        }

        public void ChangeFrequency(int newFrequency)
        {
            if (newFrequency == _frequencyThreshhold)
            {
                return;
            }

            _frequencyThreshhold = newFrequency;
            _memoryTrie = new MemoryTrie();
            FillMemoryTrie();
        }
    }
}
