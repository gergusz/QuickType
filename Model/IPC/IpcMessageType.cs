namespace QuickType.Model.IPC;

public enum IpcMessageType
{
    StatusMessage,
    SuggestionMessage,
    InputMessage,
    SelectionMessage,
    CloseMessage,
    SettingsMessage,
    SettingsRequestMessage,
    RecreateLanguageDatabaseMessage,
    ServiceShutdownMessage
}