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

namespace QuickType.Controller
{

    public static class KeyboardCapturer
    {
        private const nuint WM_KEYDOWN = 0x0100;
        private const nuint WM_SYSKEYDOWN = 0x0104;

        private static readonly ConcurrentQueue<(uint vkCode, uint scanCode, byte[] keyState)> keyQueue = new();
        private static readonly AutoResetEvent keyEvent = new(false);
        private static Thread? backgroundThread;
#pragma warning disable S1450 // Private fields only used as local variables in methods should become local variables
        private static HOOKPROC? hookCallbackDelegate;
#pragma warning restore S1450 // Private fields only used as local variables in methods should become local variables
        private static UnhookWindowsHookExSafeHandle? hookHandle;

        public delegate void KeyboardDelegate(string str);
        public static event KeyboardDelegate? KeyboardEvent;

        private static LRESULT HookProcedure(int nCode, WPARAM wParam, LPARAM lParam)
        {
            if (nCode >= 0 && (wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN))
            {
                var keyboardStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                uint vkCode = keyboardStruct.vkCode;
                uint scanCode = keyboardStruct.scanCode;
                byte[] keyState = new byte[256];
                BuildKeyState(keyState);

                keyQueue.Enqueue((vkCode, scanCode, keyState));
                keyEvent.Set();
            }

            return PInvoke.CallNextHookEx(null, nCode, wParam, lParam);
        }

        public static void Start()
        {
            backgroundThread = new Thread(ProcessKeys)
            {
                IsBackground = true
            };
            backgroundThread.Start();

            hookCallbackDelegate = HookProcedure;
            string? mainModuleName = Process.GetCurrentProcess().MainModule?.ModuleName;

            if (mainModuleName is not null)
            {
                hookHandle = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, hookCallbackDelegate, PInvoke.GetModuleHandle(mainModuleName), 0);
            }
            else
            {
                throw new InvalidOperationException("Main module name is null.");
            }
        }

        public static void Stop()
        {
            if (backgroundThread is not null)
            {
                keyEvent.Set();
                backgroundThread.Join();
                backgroundThread = null;
            }
            if (hookHandle is not null)
            {
                hookHandle.Close();
                hookHandle = null;
            }
        }

        private static void ProcessKeys()
        {
            while (true)
            {
                keyEvent.WaitOne();

                while (keyQueue.TryDequeue(out var key))
                {
                    string str = TranslateKey(key.vkCode, key.scanCode, key.keyState);
                    if (!string.IsNullOrEmpty(str) && KeyboardEvent is not null)
                    {
                        KeyboardEvent(str);
                    }
                }

                if (backgroundThread == null)
                {
                    break;
                }
            }
        }

        private static string TranslateKey(uint vkCode, uint scanCode, byte[] keyState)
        {
            switch (vkCode)
            {
                case (uint)VirtualKey.Back:
                    return "\b";
                case (uint)VirtualKey.Tab:
                    return "\t";
                case (uint)VirtualKey.Enter:
                    return "\r";
                default:
                    {
                        using UnloadKeyboardLayoutSafeHandle hkl = PInvoke.GetKeyboardLayout_SafeHandle(0);

                        char[] buffer = new char[10];
                        int chars;
                        unsafe
                        {
                            fixed (char* pBuffer = buffer)
                            {
                                chars = PInvoke.ToUnicodeEx(vkCode, scanCode, keyState, pBuffer, 10, 0, hkl);
                            }
                        }

                        if (chars > 0)
                        {
                            return new string(buffer[..chars]);
                        }
                        else
                        {
                            return string.Empty;
                        }
                    }
            }

        }

        private static void BuildKeyState(byte[] keyState)
        {
            for (int i = 0; i < 256; i++)
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
