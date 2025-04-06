using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace QuickType.Model.Trie
{
    public class HybridTrie : ITrie
    {
        private readonly MemoryTrie _memoryTrie;
        private readonly string _connectionString;
        private readonly int _frequencyThreshhold;

        public HybridTrie(string connectionString, int frequencyThreshold = 10, bool forceRecreate = false)
        {
            _memoryTrie = new MemoryTrie();
            _connectionString = connectionString;
            _frequencyThreshhold = frequencyThreshold;
            if (forceRecreate || !Path.Exists($"{connectionString.Split("=")[1]}"))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(connectionString.Split("=")[1])!);
                RecreateDatabase();
            }

            FillMemoryTrie();
        }

        private void RecreateDatabase()
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

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("QuickType.hu.csv") ?? throw new InvalidOperationException("Resource 'QuickType.hu.csv' not found.");
            using var reader = new StreamReader(stream);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    lines.Add(line);
                    
                }
            }

            using var transaction = conn.BeginTransaction();
            var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"INSERT INTO Words (Word, Frequency) VALUES ($word, $frequency)";

            var wordParam = insertCmd.CreateParameter();
            wordParam.ParameterName = "$word";
            insertCmd.Parameters.Add(wordParam);

            var freqParam = insertCmd.CreateParameter();
            freqParam.ParameterName = "$frequency";
            insertCmd.Parameters.Add(freqParam);

            for (int i = 1; i < lines.Count; i++)
            {
                var line = lines[i];

                var parts = line.Split(',');
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
                    continue;
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

            if (frequency <= _frequencyThreshhold)
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

        public bool Search(string word)
        {
            throw new NotImplementedException();
        }

        public List<Word> SearchByPrefix(string prefix, int amount = 5)
        {
            var result = _memoryTrie.SearchByPrefix(prefix, amount);

            if (result.Count < amount)
            {
                using var conn = new SqliteConnection(_connectionString);
                conn.Open();
                var selectCmd = conn.CreateCommand();
                selectCmd.CommandText = @"
                    SELECT Word, Frequency
                    FROM Words
                    WHERE Word LIKE $prefix || '%'
                    ORDER BY Frequency DESC
                    LIMIT $amount
                    ";
                selectCmd.Parameters.AddWithValue("$prefix", prefix);
                selectCmd.Parameters.AddWithValue("$amount", amount - result.Count);
                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    result.Add(new Word(reader.GetString(0), reader.GetInt32(1)));
                }
            }

            return result;
        }
    }
}
