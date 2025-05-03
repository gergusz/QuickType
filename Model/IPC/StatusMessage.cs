using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.IPC
{
    public record StatusMessage(string Status, string? Error = null) : BaseIpcMessage(IpcMessageType.StatusMessage, Error)
    {
    }
}
