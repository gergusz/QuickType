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

namespace QuickType.Controller
{
    internal static class CaretFinder
    {
        public static unsafe CaretRectangle? GetCaretPos()
        {
            HWND foregroundHWND = PInvoke.GetForegroundWindow();

            if (foregroundHWND == IntPtr.Zero)
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
            PInvoke.AccessibleObjectFromWindow(foregroundHWND, 0xFFFFFFF8, in guid, out void* ppvObject);

            var accessibleObj = Marshal.GetObjectForIUnknown((nint)ppvObject) as IAccessible;
            accessibleObj.accLocation(out int left, out int top, out int width, out int height, 0);

            CaretRectangle caretRectangle;

            if (left == 0 && top == 0 && width == 0 && height == 0)
            {
                Point caretPos = new()
                {
                    X = pgui.rcCaret.X,
                    Y = pgui.rcCaret.Y
                };
                PInvoke.ClientToScreen(pgui.hwndCaret, ref caretPos);
                caretRectangle = new(caretPos.X, caretPos.Y, pgui.rcCaret.Width, pgui.rcCaret.Height);
            } 
            else
            {
                caretRectangle = new(left, top, width, height);
            }

            if (caretRectangle.Left == 0 && caretRectangle.Top == 0 && caretRectangle.Width == 0 && caretRectangle.Height == 0)
            {
                return null;
            }

            return caretRectangle;
        }

        public readonly struct CaretRectangle(long left, long top, long width, long height)
        {
            public readonly long Left { get; } = left;
            public readonly long Top { get; } = top;
            public readonly long Width { get; } = width;
            public readonly long Height { get; } = height;
            public readonly long Right => Left + Width;
            public readonly long Bottom => Top + Height;

            public override string ToString()
            {
                return $"CaretRectangle [Left={Left}, Top={Top}, Width={Width}, Height={Height}, Right={Right}, Bottom={Bottom}]";
            }
        }
    }
}
