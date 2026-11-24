using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Clickold;

internal sealed class WindowsInput : IDisposable
{
    private readonly HookProc keyboardCallback;
    private readonly HookProc mouseCallback;
    private readonly Func<KeyboardEvent, bool> onKeyboard;
    private readonly Action<MacroEvent> onMouse;
    private nint keyboardHook;
    private nint mouseHook;

    public bool CaptureMouse { get; set; }

    public WindowsInput(Func<KeyboardEvent, bool> onKeyboard, Action<MacroEvent> onMouse)
    {
        this.onKeyboard = onKeyboard;
        this.onMouse = onMouse;
        keyboardCallback = KeyboardCallback;
        mouseCallback = MouseCallback;
        var module = GetModuleHandle(null);

        keyboardHook = SetWindowsHookEx(13, keyboardCallback, module, 0);
        if (keyboardHook == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        mouseHook = SetWindowsHookEx(14, mouseCallback, module, 0);
        if (mouseHook == 0)
        {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            Dispose();
            throw error;
        }
    }

    private nint KeyboardCallback(int code, nint message, nint data)
    {
        if (code == 0)
        {
            var item = Marshal.PtrToStructure<KeyboardData>(data);
            const uint injected = 0x10;
            if ((item.Flags & injected) == 0)
            {
                var keyboard = new KeyboardEvent((int)item.VirtualKey, (int)item.ScanCode,
                    (item.Flags & 0x01) != 0, (item.Flags & 0x80) != 0);
                if (onKeyboard(keyboard))
                    return 1;
            }
        }
        return CallNextHookEx(0, code, message, data);
    }

    private nint MouseCallback(int code, nint message, nint data)
    {
        if (code == 0 && CaptureMouse)
        {
            var item = Marshal.PtrToStructure<MouseData>(data);
            const uint injected = 0x01;
            if ((item.Flags & injected) == 0 && DecodeMouse((int)message, item) is { } action)
                onMouse(action);
        }
        return CallNextHookEx(0, code, message, data);
    }

    private static MacroEvent? DecodeMouse(int message, MouseData data)
    {
        short payload = unchecked((short)(data.MouseDataValue >> 16));
        var extraButton = payload == 1 ? MouseButton.X1 : MouseButton.X2;
        (InputKind Kind, int Code)? action = message switch
        {
            0x0200 => (InputKind.MouseMove, 0),
            0x0201 => (InputKind.MouseDown, (int)MouseButton.Left),
            0x0202 => (InputKind.MouseUp, (int)MouseButton.Left),
            0x0204 => (InputKind.MouseDown, (int)MouseButton.Right),
            0x0205 => (InputKind.MouseUp, (int)MouseButton.Right),
            0x0207 => (InputKind.MouseDown, (int)MouseButton.Middle),
            0x0208 => (InputKind.MouseUp, (int)MouseButton.Middle),
            0x020A => (InputKind.Wheel, payload),
            0x020B => (InputKind.MouseDown, (int)extraButton),
            0x020C => (InputKind.MouseUp, (int)extraButton),
            0x020E => (InputKind.HorizontalWheel, payload),
            _ => null
        };
        return action is { } value
            ? new MacroEvent(0, value.Kind, value.Code, X: data.Position.X, Y: data.Position.Y)
            : null;
    }

    public static bool IsOverWindow(nint window, int x, int y) =>
        GetAncestor(WindowFromPoint(new Point(x, y)), 2) == window;

    public static bool IsForeground(nint window) => GetForegroundWindow() == window;

    public static void Replay(MacroEvent item, bool positionMouse = true)
    {
        Input[] inputs;
        if (item.Kind is InputKind.KeyDown or InputKind.KeyUp)
        {
            // Pause and Print Screen use special scan-code sequences.
            bool useScan = item.Scan != 0 && item.Code != (int)Keys.Pause && item.Code != (int)Keys.PrintScreen;
            inputs = [new Input
            {
                Type = 1,
                Data = new InputData
                {
                    Keyboard = new KeyboardInput
                    {
                        VirtualKey = useScan ? (ushort)0 : (ushort)item.Code,
                        ScanCode = useScan ? (ushort)item.Scan : (ushort)0,
                        Flags = (item.Extended ? 1u : 0u) | (item.Kind == InputKind.KeyUp ? 2u : 0u)
                            | (useScan ? 8u : 0u)
                    }
                }
            }];
        }
        else if (item.Kind == InputKind.MouseMove)
        {
            inputs = [MouseMove(item)];
        }
        else
        {
            var action = MouseAction(item);
            inputs = positionMouse ? [MouseMove(item), action] : [action];
        }

        if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), UiText.Get(TextId.InputRejected));
    }

    private static Input MouseMove(MacroEvent item)
    {
        var screen = SystemInformation.VirtualScreen;
        return new Input
        {
            Data = new InputData
            {
                Mouse = new MouseInput
                {
                    Dx = Normalize(item.X, screen.Left, screen.Width),
                    Dy = Normalize(item.Y, screen.Top, screen.Height),
                    Flags = 0x8000 | 0x4000 | 0x0001
                }
            }
        };
    }

    private static int Normalize(int coordinate, int origin, int size) =>
        (int)Math.Clamp((((long)coordinate - origin) * 65536 + 32768) / Math.Max(1, size), 0, 65535);

    private static Input MouseAction(MacroEvent item)
    {
        bool down = item.Kind == InputKind.MouseDown;
        var (flags, payload) = item.Kind switch
        {
            InputKind.Wheel => (0x0800u, unchecked((uint)item.Code)),
            InputKind.HorizontalWheel => (0x1000u, unchecked((uint)item.Code)),
            _ => (MouseButton)item.Code switch
            {
                MouseButton.Left => (down ? 0x0002u : 0x0004u, 0u),
                MouseButton.Right => (down ? 0x0008u : 0x0010u, 0u),
                MouseButton.Middle => (down ? 0x0020u : 0x0040u, 0u),
                MouseButton.X1 => (down ? 0x0080u : 0x0100u, 1u),
                MouseButton.X2 => (down ? 0x0080u : 0x0100u, 2u),
                _ => throw new ArgumentOutOfRangeException(nameof(item))
            }
        };
        return new Input { Data = new InputData { Mouse = new MouseInput { Flags = flags, Data = payload } } };
    }

    public void Dispose()
    {
        if (mouseHook != 0)
            UnhookWindowsHookEx(mouseHook);
        if (keyboardHook != 0)
            UnhookWindowsHookEx(keyboardHook);
        mouseHook = keyboardHook = 0;
    }

    internal readonly record struct KeyboardEvent(int Key, int Scan, bool Extended, bool Up);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct KeyboardData
    {
        public readonly uint VirtualKey, ScanCode, Flags, Time;
        public readonly nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MouseData
    {
        public readonly Point Position;
        public readonly uint MouseDataValue, Flags, Time;
        public readonly nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx, Dy;
        public uint Data, Flags, Time;
        public nuint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey, ScanCode;
        public uint Flags, Time;
        public nuint ExtraInfo;
    }

    private delegate nint HookProc(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);

    [DllImport("user32.dll")]
    private static extern nint WindowFromPoint(Point point);

    [DllImport("user32.dll")]
    private static extern nint GetAncestor(nint window, uint flags);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, [In] Input[] inputs, int size);
}
