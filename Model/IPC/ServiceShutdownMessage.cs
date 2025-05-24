namespace QuickType.Model.IPC;
public record ServiceShutdownMessage(string? Error = null) : BaseIpcMessage(IpcMessageType.ServiceShutdownMessage, Error)
{
}