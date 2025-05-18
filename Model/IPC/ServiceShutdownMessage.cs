using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Languages;

namespace QuickType.Model.IPC;
public record ServiceShutdownMessage(string? Error = null) : BaseIpcMessage(IpcMessageType.ServiceShutdownMessage, Error)
{
}