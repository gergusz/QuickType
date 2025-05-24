using System.Collections.Generic;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace QuickType.Services;

public class InputSimulatorService
{
    public void SimulateInputString(string input, bool wasCtrlUsed)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        var inputList = new List<INPUT>();

        if (wasCtrlUsed)
        {
            var simulatedCtrlUp = new INPUT()
            {
                type = INPUT_TYPE.INPUT_KEYBOARD,
                Anonymous = new INPUT._Anonymous_e__Union
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = VIRTUAL_KEY.VK_CONTROL,
                        wScan = 0,
                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                        time = 0,
                        dwExtraInfo = 0
                    }
                }
            };

            inputList.Add(simulatedCtrlUp);
        }

        foreach (var character in input)
        {
            var simulatedKeyDown = new INPUT()
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
            var simulatedKeyUp = new INPUT()
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
            inputList.Add(simulatedKeyDown);
            inputList.Add(simulatedKeyUp);
        }

        PInvoke.SendInput(CollectionsMarshal.AsSpan(inputList), Marshal.SizeOf<INPUT>());

    }
}