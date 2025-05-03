using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.IPC
{
    public record SettingsRequestMessage(bool Reset = false, string? Error = null) : BaseIpcMessage(IpcMessageType.SettingsRequestMessage, Error)
    {
    }
}