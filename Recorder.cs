using System.Diagnostics;

namespace Clickold;

internal sealed class Recorder
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly List<MacroEvent> events = [];
    private readonly HashSet<int> keys = [];
    private readonly HashSet<int> buttons = [];
    private long firstEventTime;
    private long lastDragPoint = -12;

    public int Count => events.Count;

    public void Keyboard(WindowsInput.KeyboardEvent item, bool ownWindow)
    {
        if (item.Up)
        {
            if (!keys.Remove(item.Key))
                return;
        }
        else
        {
            if (ownWindow)
                return;
            keys.Add(item.Key);
        }

        var kind = item.Up ? InputKind.KeyUp : InputKind.KeyDown;
        Add(new MacroEvent(0, kind, item.Key, item.Scan, item.Extended));
    }

    public void Mouse(MacroEvent item, nint window)
    {
        switch (item.Kind)
        {
            case InputKind.MouseMove:
                if (buttons.Count == 0 || clock.ElapsedMilliseconds - lastDragPoint < 12)
                    return;
                lastDragPoint = clock.ElapsedMilliseconds;
                break;
            case InputKind.MouseUp:
                if (!buttons.Remove(item.Code))
                    return;
                break;
            default:
                if (WindowsInput.IsOverWindow(window, item.X, item.Y))
                    return;
                if (item.Kind == InputKind.MouseDown)
                    buttons.Add(item.Code);
                break;
        }
        Add(item);
    }

    private void Add(MacroEvent item)
    {
        long now = clock.ElapsedMilliseconds;
        if (events.Count == 0)
            firstEventTime = now;
        events.Add(item with { TimeMs = now - firstEventTime });
    }

    public Macro Finish() => new(1, [.. events]);
}
