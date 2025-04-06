using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickType.Controller
{
    public static class InputSimulator
    {
        public static void SimulateInputString(string input)
        {
            foreach (char character in input)
            {
                var InputList = new List<INPUT>();
                var SimulatedKeyDown = new INPUT()
                {
                    type = INPUT_TYPE.INPUT_KEYBOARD,
                    Anonymous = new INPUT._Anonymous_e__Union
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = character,
                            dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE,
                            time = 0,
                            dwExtraInfo = 0
                        }
                    }
                };
                var SimulatedKeyUp = new INPUT()
                {
                    type = INPUT_TYPE.INPUT_KEYBOARD,
                    Anonymous = new INPUT._Anonymous_e__Union
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = 0,
                            wScan = character,
                            dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                            time = 0,
                            dwExtraInfo = 0
                        }
                    }
                };
                InputList.Add(SimulatedKeyDown);
                InputList.Add(SimulatedKeyUp);
                PInvoke.SendInput(CollectionsMarshal.AsSpan(InputList), Marshal.SizeOf<INPUT>());
                //Thread.Sleep(10);
            }
        }
    }
}
