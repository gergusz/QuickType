using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.IPC
{
    public record BaseIpcMessage(IpcMessageType Type, string? Error = null)
    {
        public bool IsError => !string.IsNullOrEmpty(Error);
    }
}
