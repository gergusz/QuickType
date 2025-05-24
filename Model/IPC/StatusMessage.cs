namespace QuickType.Model.IPC;

public record StatusMessage(string Status, string? Error = null) : BaseIpcMessage(IpcMessageType.StatusMessage, Error)
{
}