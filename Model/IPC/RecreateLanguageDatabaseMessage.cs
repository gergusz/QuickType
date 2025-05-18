using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Languages;

namespace QuickType.Model.IPC;
public record RecreateLanguageDatabaseMessage(CustomLanguageDefinition Language, string? Error = null) : BaseIpcMessage(IpcMessageType.RecreateLanguageDatabaseMessage, Error)
{
}
