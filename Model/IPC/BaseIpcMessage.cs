namespace QuickType.Model.IPC;

public record BaseIpcMessage(IpcMessageType Type, string? Error = null)
{
    public bool IsError => !string.IsNullOrEmpty(Error);
}