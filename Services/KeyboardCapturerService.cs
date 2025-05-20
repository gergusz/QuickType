using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace QuickType.Services
{
    public class KeyboardCapturerService
    {
        private const nuint WM_KEYDOWN = 0x0100;
        private const nuint WM_SYSKEYDOWN = 0x0104;

        private readonly ConcurrentQueue<(uint vkCode, uint scanCode, byte[] keyState)> _keyQueue = new();
        private readonly AutoResetEvent _keyEvent = new(false);
        private Thread? _backgroundThread;
#pragma warning disable S1450 // Private fields only used as local variables in methods should become local variables
        private HOOKPROC? _hookCallbackDelegate;
#pragma warning restore S1450 // Private fields only used as local variables in methods should become local variables
        private UnhookWindowsHookExSafeHandle? _hookHandle;

        public delegate void KeyboardDelegate(string str);
        public event KeyboardDelegate? KeyboardEvent;

        public bool AreSuggestionsShowing = false;

        private LRESULT HookProcedure(int nCode, WPARAM wParam, LPARAM lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                var keyboardStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                var vkCode = keyboardStruct.vkCode;
                var scanCode = keyboardStruct.scanCode;
                var keyState = new byte[256];
                BuildKeyState(keyState);

                var isCtrlDown = (keyState[(int)VirtualKey.Control] & 0x80) != 0;
                var isNumber = vkCode is >= (uint)VirtualKey.Number0 and <= (uint)VirtualKey.Number9 ||
                                vkCode is >= (uint)VirtualKey.NumberPad0 and <= (uint)VirtualKey.NumberPad9;

                _keyQueue.Enqueue((vkCode, scanCode, keyState));
                _keyEvent.Set();

                if (isCtrlDown && isNumber && AreSuggestionsShowing)
                {
                    return new LRESULT(1);
                }
            }

            return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
        }

        public void Start()
        {
            try
            {
                _backgroundThread = new Thread(ProcessKeys)
                {
                    IsBackground = true
                };
                _backgroundThread.Start();

                _hookCallbackDelegate = HookProcedure;

                var mainModuleName = Process.GetCurrentProcess().MainModule!.ModuleName;
                if (mainModuleName is null)
                {
                    throw new InvalidOperationException("Main module name is null.");
                }

                var moduleHandle = PInvoke.GetModuleHandle(mainModuleName);
                _hookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookCallbackDelegate, moduleHandle, 0);

                if (_hookHandle == null || _hookHandle.IsInvalid)
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException($"Failed to set keyboard hook. Error code: {errorCode}");
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in KeyboardCapturerService.Start: {ex.Message}");
                Stop();
                throw;
            }
        }

        public void Stop()
        {
            if (_backgroundThread is not null)
            {
                _keyEvent.Set();
                _backgroundThread.Join();
                _backgroundThread = null;
            }
            if (_hookHandle is not null)
            {
                _hookHandle.Close();
                _hookHandle = null;
            }
        }

        private void ProcessKeys()
        {
            while (true)
            {
                _keyEvent.WaitOne();

                while (_keyQueue.TryDequeue(out var key))
                {
                    var str = TranslateKey(key.vkCode, key.scanCode, key.keyState);
                    if (!string.IsNullOrEmpty(str) && KeyboardEvent is not null)
                    {
                        KeyboardEvent(str);
                    }
                }

                if (_backgroundThread == null)
                {
                    break;
                }
            }
        }

        private string TranslateKey(uint vkCode, uint scanCode, byte[] keyState)
        {
            switch (vkCode)
            {
                case (uint)VirtualKey.Back:
                    return "\b";
                case (uint)VirtualKey.Tab:
                    return "\t";
                case (uint)VirtualKey.Enter:
                    return "\r";
                case (uint)VirtualKey.Escape:
                    return "\n";
                case (uint)VirtualKey.Number0:
                case (uint)VirtualKey.Number1:
                case (uint)VirtualKey.Number2:
                case (uint)VirtualKey.Number3:
                case (uint)VirtualKey.Number4:
                case (uint)VirtualKey.Number5:
                case (uint)VirtualKey.Number6:
                case (uint)VirtualKey.Number7:
                case (uint)VirtualKey.Number8:
                case (uint)VirtualKey.Number9:
                case (uint)VirtualKey.NumberPad0:
                case (uint)VirtualKey.NumberPad1:
                case (uint)VirtualKey.NumberPad2:
                case (uint)VirtualKey.NumberPad3:
                case (uint)VirtualKey.NumberPad4:
                case (uint)VirtualKey.NumberPad5:
                case (uint)VirtualKey.NumberPad6:
                case (uint)VirtualKey.NumberPad7:
                case (uint)VirtualKey.NumberPad8:
                case (uint)VirtualKey.NumberPad9:
                    return (keyState[(int)VirtualKey.Control] & 0x80) != 0 ? $@"\c{(char)vkCode}" : GetUnicodeChar();
                default:
                {
                    return GetUnicodeChar();
                }
            }

            string GetUnicodeChar()
            {
                using UnloadKeyboardLayoutSafeHandle hkl = PInvoke.GetKeyboardLayout_SafeHandle(0);

                ReadOnlySpan<byte> keyStateSpan = new(keyState);
                Span<char> buffer = new char[10];

                var chars = PInvoke.ToUnicodeEx(vkCode, scanCode, keyStateSpan, buffer, 0, hkl);

                return chars > 0 ? new(buffer[..chars]) : string.Empty;
            }
        }

        private void BuildKeyState(byte[] keyState)
        {
            for (var i = 0; i < 256; i++)
            {
                int state = PInvoke.GetAsyncKeyState(i);
                if ((state & 0x8000) != 0)
                {
                    keyState[i] = 0x80;
                }
                else
                {
                    keyState[i] = 0x00;
                }
            }
        }
    }
}
