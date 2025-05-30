﻿using QuickType.Model;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace QuickType.Services;

public class CaretFinderService
{
    private const uint OBJID_CARET = 0xFFFFFFF8;

    public unsafe CaretRectangle? GetCaretPosition()
    {
        var foregroundHwnd = PInvoke.GetForegroundWindow();

        if (foregroundHwnd == nint.Zero)
        {
            return null;
        }

        GUITHREADINFO pgui = new()
        {
            cbSize = (uint)Marshal.SizeOf<GUITHREADINFO>()
        };
            
        if (!PInvoke.GetGUIThreadInfo(0, ref pgui))
        {
            return null;
        }

        var guid = typeof(IAccessible).GUID;
        var hresult = PInvoke.AccessibleObjectFromWindow(foregroundHwnd, OBJID_CARET, in guid, out var ppvObject);

        if (hresult.Succeeded && ppvObject is not null)
        {
            if (Marshal.GetObjectForIUnknown((nint)ppvObject) is IAccessible accessibleObj)
            {
                accessibleObj.accLocation(out var left, out var top, out var width, out var height, 0);

                if (left != 0 || top != 0 || width != 0 || height != 0)
                {
                    Marshal.Release((nint)ppvObject);

                    return new CaretRectangle(left, top, width, height);
                }
            }

            Marshal.Release((nint)ppvObject);
        }

        Point caretPos = new()
        { 
            X = pgui.rcCaret.X,
            Y = pgui.rcCaret.Y
        };
        PInvoke.ClientToScreen(pgui.hwndCaret, ref caretPos);

        if (pgui.rcCaret is { Width: 0, Height: 0 } || caretPos is { X: 0, Y: 0 } )
        {
            return GetMousePosition();
        }

        return new CaretRectangle(caretPos.X, caretPos.Y, pgui.rcCaret.Width, pgui.rcCaret.Height);
    }

    private CaretRectangle? GetMousePosition()
    {
        if (PInvoke.GetCursorPos(out var mousePos) == 0)
        {
            return null;
        }


        return new CaretRectangle(
            mousePos.X,
            mousePos.Y,
            1,
            1
        );
    }
}