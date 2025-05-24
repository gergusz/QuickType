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
