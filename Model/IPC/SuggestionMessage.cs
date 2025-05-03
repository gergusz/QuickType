using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using QuickType.Model.Trie;

namespace QuickType.Model.IPC
{
    public record SuggestionMessage(List<Word> Suggestions, CaretRectangle? CaretPosition, string? Error = null) : BaseIpcMessage(IpcMessageType.SuggestionMessage, Error)
    {
    }
}
