using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickType.WinUI;
public class LanguageViewModel
{
    public string Name
    {
        get; 
        set;
    } = string.Empty;

    public string? InternalName
    {
        get;
        set;
    }

    public string Description
    {
        get; 
        set;
    } = string.Empty;

    public int Priority
    {
        get;
        set;
    }

    public bool IsCustom
    {
        get;
        init;
    } = true;
}