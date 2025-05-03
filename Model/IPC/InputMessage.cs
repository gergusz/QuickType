using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.IPC
{
    public record InputMessage(string Input, string? Error = null) : BaseIpcMessage(IpcMessageType.InputMessage, Error)
    {
    }
}
