using System.ComponentModel;
using System.Diagnostics;

namespace Clickold;

internal sealed class Playback
{
    private readonly Stopwatch clock = new();
    private readonly Dictionary<int, MacroEvent> heldKeys = [];
    private readonly Dictionary<int, MacroEvent> heldButtons = [];
    private TaskCompletionSource? resume;

    public bool IsPaused => resume is not null;

    public void TogglePause()
    {
        if (resume is null)
        {
            clock.Stop();
            resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        else
        {
            var signal = resume;
            resume = null;
            clock.Start();
            signal.SetResult();
        }
    }

    public async Task Run(
        Macro macro, double speed, int repetitions, bool loop, bool waitForFocus,
        Action<long> report, CancellationToken cancellation)
    {
        if (waitForFocus)
            await Task.Delay(100, cancellation);

        for (long completed = 0; loop || completed < repetitions;)
        {
            clock.Reset();
            if (!IsPaused)
                clock.Start();

            try
            {
                for (int index = 0; index < macro.Events.Length; index++)
                {
                    if (index > 0 && index % 32 == 0)
                        await Task.Delay(1, cancellation);
                    var item = macro.Events[index];
                    await WaitUntil(item.TimeMs / speed, cancellation);
                    Send(item);
                }
            }
            finally
            {
                ReleaseHeldInputs();
            }

            report(++completed);
            if (loop || completed < repetitions)
                await Task.Delay(15, cancellation);
        }
    }

    private async Task WaitUntil(double target, CancellationToken cancellation)
    {
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (resume is { } signal)
            {
                await signal.Task.WaitAsync(cancellation);
                continue;
            }
            double remaining = target - clock.Elapsed.TotalMilliseconds;
            if (remaining <= 0)
                return;
            await Task.Delay((int)Math.Clamp(Math.Ceiling(remaining), 1, 50), cancellation);
        }
    }

    private void Send(MacroEvent item)
    {
        WindowsInput.Replay(item);
        switch (item.Kind)
        {
            case InputKind.KeyDown: heldKeys[item.Code] = item; break;
            case InputKind.KeyUp: heldKeys.Remove(item.Code); break;
            case InputKind.MouseDown: heldButtons[item.Code] = item; break;
            case InputKind.MouseUp: heldButtons.Remove(item.Code); break;
        }
    }

    private void ReleaseHeldInputs()
    {
        Win32Exception? failure = null;
        foreach (var item in heldButtons.Values.Concat(heldKeys.Values))
        {
            var kind = item.Kind == InputKind.KeyDown ? InputKind.KeyUp : InputKind.MouseUp;
            try
            {
                WindowsInput.Replay(item with { Kind = kind }, positionMouse: false);
            }
            catch (Win32Exception error)
            {
                failure ??= error;
            }
        }
        heldButtons.Clear();
        heldKeys.Clear();
        if (failure is not null)
            throw failure;
    }
}
