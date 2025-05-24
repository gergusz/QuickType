namespace QuickType.Model.IPC;

public record SettingsMessage(AppSettings Settings, string? Error = null) : BaseIpcMessage(IpcMessageType.SettingsMessage, Error)
{
}