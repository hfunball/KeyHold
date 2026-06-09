using System.Runtime.InteropServices;
using RunHold.Models;

namespace RunHold.Services;

public sealed class KeyboardHookService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int LlkhfInjected = 0x10;
    private const int LlkhfAltdown = 0x20;

    private readonly RunHoldEngine engine;
    private readonly LowLevelKeyboardProc callback;
    private IntPtr hookId;

    public KeyboardHookService(RunHoldEngine engine)
    {
        this.engine = engine;
        callback = HookCallback;
    }

    public void Start()
    {
        if (hookId != IntPtr.Zero)
        {
            return;
        }

        hookId = SetWindowsHookEx(WhKeyboardLl, callback, IntPtr.Zero, 0);
        if (hookId == IntPtr.Zero)
        {
            engine.LogDiagnostic($"Keyboard hook failed to start. Win32 error: {Marshal.GetLastWin32Error()}.");
        }
    }

    public void Dispose()
    {
        if (hookId == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(hookId);
        hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        var message = wParam.ToInt32();
        var isDown = message is WmKeyDown or WmSysKeyDown;
        var isUp = message is WmKeyUp or WmSysKeyUp;

        if (!isDown && !isUp)
        {
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
        var isInjected = (data.Flags & LlkhfInjected) == LlkhfInjected;
        var isRunHoldInjected = data.ExtraInfo == InputInjectionMarker.RunHoldInput;
        var acceptExternalInjectedInput = string.Equals(
            Environment.GetEnvironmentVariable(InputInjectionMarker.AcceptExternalInjectedInputEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
        var input = new KeyboardInputEvent(
            data.VirtualKey,
            isDown,
            isInjected && (isRunHoldInjected || !acceptExternalInjectedInput),
            (data.Flags & LlkhfAltdown) == LlkhfAltdown);

        return engine.HandleKeyboardEvent(input)
            ? new IntPtr(1)
            : CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, LowLevelKeyboardProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public int VirtualKey;
        public int ScanCode;
        public int Flags;
        public int Time;
        public UIntPtr ExtraInfo;
    }
}
