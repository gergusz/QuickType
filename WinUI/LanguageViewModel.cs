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

    private string? _internalName;

    public string? InternalName
    {
        get => _internalName ?? Name;
        set => _internalName = value;
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