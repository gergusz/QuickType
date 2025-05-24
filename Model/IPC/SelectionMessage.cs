namespace QuickType.Model.IPC;

public record SelectionMessage(int? Placement, string? Word, string? Error = null) : BaseIpcMessage(IpcMessageType.SelectionMessage, Error)
{
}