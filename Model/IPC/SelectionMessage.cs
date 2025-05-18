using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.IPC
{
    public record SelectionMessage(int? Placement, string? Word, string? Error = null) : BaseIpcMessage(IpcMessageType.SelectionMessage, Error)
    {
    }
}
