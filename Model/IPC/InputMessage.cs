namespace QuickType.Model.IPC;

public record InputMessage(string Input, string? Error = null) : BaseIpcMessage(IpcMessageType.InputMessage, Error)
{
}