using System.Runtime.InteropServices;

namespace RunHold.Services;

public sealed class SendInputService : IInputSender
{
    private const int InputKeyboard = 1;
    private const uint KeyEventFExtendedKey = 0x0001;
    private const uint KeyEventFKeyUp = 0x0002;
    private const uint KeyEventFScanCode = 0x0008;
    private const uint MapVkToVsc = 0;

    public void SendKeyDown(int virtualKey)
    {
        SendKey(virtualKey, 0);
    }

    public void SendKeyUp(int virtualKey)
    {
        SendKey(virtualKey, KeyEventFKeyUp);
        SendVirtualKey(virtualKey, KeyEventFKeyUp);
        var scanCode = (byte)(MapVirtualKey((uint)virtualKey, MapVkToVsc) & 0xFF);
        keybd_event(
            (byte)virtualKey,
            scanCode,
            KeyEventFKeyUp | (IsExtendedKey(virtualKey) ? KeyEventFExtendedKey : 0),
            InputInjectionMarker.RunHoldInput);
    }

    private static void SendKey(int virtualKey, uint flags)
    {
        var scanCode = (ushort)MapVirtualKey((uint)virtualKey, MapVkToVsc);
        if (scanCode == 0)
        {
            keybd_event((byte)virtualKey, 0, flags, InputInjectionMarker.RunHoldInput);
            return;
        }

        var input = new NativeInput
        {
            Type = InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new KeyboardInput
                {
                    ScanCode = scanCode,
                    Flags = KeyEventFScanCode | flags | (IsExtendedKey(virtualKey) ? KeyEventFExtendedKey : 0),
                    ExtraInfo = InputInjectionMarker.RunHoldInput
                }
            }
        };

        if (SendInput(1, [input], Marshal.SizeOf<NativeInput>()) == 0)
        {
            keybd_event((byte)virtualKey, 0, flags, InputInjectionMarker.RunHoldInput);
        }
    }

    private static void SendVirtualKey(int virtualKey, uint flags)
    {
        var nativeFlags = flags | (IsExtendedKey(virtualKey) ? KeyEventFExtendedKey : 0);
        var input = new NativeInput
        {
            Type = InputKeyboard,
            Data = new NativeInputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = (ushort)virtualKey,
                    Flags = nativeFlags,
                    ExtraInfo = InputInjectionMarker.RunHoldInput
                }
            }
        };

        if (SendInput(1, [input], Marshal.SizeOf<NativeInput>()) == 0)
        {
            keybd_event((byte)virtualKey, 0, nativeFlags, InputInjectionMarker.RunHoldInput);
        }
    }

    private static bool IsExtendedKey(int virtualKey)
    {
        return virtualKey is 0x21 or 0x22 or 0x23 or 0x24 or 0x25 or 0x26 or 0x27 or 0x28
            or 0x2D or 0x2E or 0x5B or 0x5C or 0x6F
            or 0x90 or 0xA3 or 0xA5;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, NativeInput[] inputs, int inputSize);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint code, uint mapType);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeInput
    {
        public int Type;
        public NativeInputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct NativeInputUnion
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
