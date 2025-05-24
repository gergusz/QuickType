using QuickType.Model.Languages;

namespace QuickType.Model.IPC;
public record RecreateLanguageDatabaseMessage(CustomLanguageDefinition Language, string? Error = null) : BaseIpcMessage(IpcMessageType.RecreateLanguageDatabaseMessage, Error)
{
}
