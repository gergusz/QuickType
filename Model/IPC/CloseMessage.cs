namespace QuickType.Model.IPC;
public record CloseMessage(string? Error = null) : BaseIpcMessage(IpcMessageType.CloseMessage, Error)
{
}
