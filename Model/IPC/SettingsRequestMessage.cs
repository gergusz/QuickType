namespace QuickType.Model.IPC;

public record SettingsRequestMessage(bool Reset = false, string? Error = null) : BaseIpcMessage(IpcMessageType.SettingsRequestMessage, Error)
{
}