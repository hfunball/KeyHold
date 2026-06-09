using System.Runtime.InteropServices;
using RunHold.Models;

namespace RunHold.Services;

public sealed class MouseHookService : IDisposable
{
    private const int WhMouseLl = 14;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const int LlmhfInjected = 0x00000001;
    private const int XButton2 = 2;

    private readonly RunHoldEngine engine;
    private readonly LowLevelMouseProc callback;
    private IntPtr hookId;

    public MouseHookService(RunHoldEngine engine)
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

        hookId = SetWindowsHookEx(WhMouseLl, callback, IntPtr.Zero, 0);
        if (hookId == IntPtr.Zero)
        {
            engine.LogDiagnostic($"Mouse hook failed to start. Win32 error: {Marshal.GetLastWin32Error()}.");
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
        var isDown = message is WmMiddleButtonDown or WmXButtonDown;
        var isUp = message is WmMiddleButtonUp or WmXButtonUp;

        if (!isDown && !isUp)
        {
            return CallNextHookEx(hookId, nCode, wParam, lParam);
        }

        var data = Marshal.PtrToStructure<MouseLlHookStruct>(lParam);
        var isInjected = (data.Flags & LlmhfInjected) == LlmhfInjected;
        var acceptExternalInjectedInput = string.Equals(
            Environment.GetEnvironmentVariable(InputInjectionMarker.AcceptExternalInjectedInputEnvironmentVariable),
            "1",
            StringComparison.Ordinal);
        var button = message switch
        {
            WmMiddleButtonDown or WmMiddleButtonUp => MouseTriggerCode.Middle,
            WmXButtonDown or WmXButtonUp => GetHighWord(data.MouseData) == XButton2 ? MouseTriggerCode.XButton2 : MouseTriggerCode.XButton1,
            _ => MouseTriggerCode.Middle
        };

        var input = new MouseInputEvent(button, isDown, isInjected && !acceptExternalInjectedInput);
        return engine.HandleMouseEvent(input)
            ? new IntPtr(1)
            : CallNextHookEx(hookId, nCode, wParam, lParam);
    }

    private static int GetHighWord(int value)
    {
        return (value >> 16) & 0xffff;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, LowLevelMouseProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseLlHookStruct
    {
        public Point Point;
        public int MouseData;
        public int Flags;
        public int Time;
        public UIntPtr ExtraInfo;
    }
}
