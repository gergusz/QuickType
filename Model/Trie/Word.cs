namespace QuickType.Model.Trie;

public record struct Word(string word, int frequency)
{
    public static implicit operator (string word, int frequency)(Word value)
    {
        return (value.word, value.frequency);
    }

    public static implicit operator Word((string word, int frequency) value)
    {
        return new Word(value.word, value.frequency);
    }
}