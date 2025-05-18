using QuickType.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace QuickType.Services
{
    public class CaretFinderService
    {
        public unsafe CaretRectangle? GetCaretPos()
        {
            HWND foregroundHWND = PInvoke.GetForegroundWindow();

            if (foregroundHWND == nint.Zero)
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

            Guid guid = typeof(IAccessible).GUID;
            HRESULT hresult = PInvoke.AccessibleObjectFromWindow(foregroundHWND, 0xFFFFFFF8, in guid, out void* ppvObject);


            if (hresult.Succeeded && ppvObject is not null)
            {
                if (Marshal.GetObjectForIUnknown((nint)ppvObject) is IAccessible accessibleObj)
                {
                    accessibleObj.accLocation(out int left, out int top, out int width, out int height, 0);

                    if (left != 0 || top != 0 || width != 0 || height != 0)
                    {
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
                return null;
            }

            return new CaretRectangle(caretPos.X, caretPos.Y, pgui.rcCaret.Width, pgui.rcCaret.Height);
        }
    }
}
