using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.Model.Languages;
public class InternalLanguageDefinition
{
    public string Name
    {
        get;
        init;
    }

    public int Priority
    {
        get;
        set;
    }
    public InternalLanguageDefinition(
        string name,
        int priority)
    {
        Name = name;
        Priority = priority;
    }
}
