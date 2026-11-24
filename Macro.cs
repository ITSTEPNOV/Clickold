using System.Text.Json;
using System.Text.Json.Serialization;

namespace Clickold;

internal enum InputKind
{
    KeyDown, KeyUp, MouseDown, MouseUp, MouseMove, Wheel, HorizontalWheel
}

internal enum MouseButton
{
    Left = 1, Right, Middle, X1, X2
}

internal sealed record MacroEvent(
    [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] long TimeMs,
    [property: JsonRequired, JsonIgnore(Condition = JsonIgnoreCondition.Never)] InputKind Kind,
    int Code = 0, int Scan = 0, bool Extended = false,
    int X = 0, int Y = 0);

internal sealed record Macro(int Version, MacroEvent[] Events)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
    };

    public static Macro Load(string path)
    {
        using var file = File.OpenRead(path);
        var macro = JsonSerializer.Deserialize<Macro>(file, JsonOptions);
        if (macro is not { Version: 1, Events.Length: > 0 })
            throw new InvalidDataException(UiText.Get(TextId.MacroUnsupported));

        long previous = 0;
        foreach (var item in macro.Events)
        {
            if (item is null || item.TimeMs < previous || !IsValid(item))
                throw new InvalidDataException(UiText.Get(TextId.MacroInvalidEvent));
            previous = item.TimeMs;
        }
        return macro;
    }

    private static bool IsValid(MacroEvent item) => item.Kind switch
    {
        InputKind.KeyDown or InputKind.KeyUp =>
            item.Code is >= 1 and <= 254 && item.Scan is >= 0 and <= ushort.MaxValue,
        InputKind.MouseDown or InputKind.MouseUp => Enum.IsDefined((MouseButton)item.Code),
        InputKind.Wheel or InputKind.HorizontalWheel => item.Code is >= short.MinValue and <= short.MaxValue,
        InputKind.MouseMove => true,
        _ => false
    };

    public void Save(string path)
    {
        var temporary = Path.Combine(Path.GetDirectoryName(path)!, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = File.Create(temporary))
                JsonSerializer.Serialize(file, this, JsonOptions);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}

internal sealed record Hotkeys(int Start, int Stop, int Pause, int RecordStart = 0, int RecordStop = 0)
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Clickold", "hotkeys.json");

    public static Hotkeys Default { get; } = new((int)Keys.F6, (int)Keys.F7, (int)Keys.F8,
        (int)Keys.F9, (int)Keys.F10);

    [JsonIgnore]
    public bool IsValid
    {
        get
        {
            int[] keys = [Start, Stop, Pause, RecordStart, RecordStop];
            return keys.All(IsFunctionKey) && keys.Distinct().Count() == keys.Length;
        }
    }

    public bool Contains(int key) => key == Start || key == Stop || key == Pause
        || key == RecordStart || key == RecordStop;

    public static Hotkeys Load()
    {
        try
        {
            using var file = File.OpenRead(SettingsPath);
            var value = JsonSerializer.Deserialize<Hotkeys>(file)?.WithRecordingDefaults();
            return value is { IsValid: true } ? value : Default;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return Default;
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this));
    }

    private Hotkeys WithRecordingDefaults()
    {
        var value = this;
        if (value.RecordStart == 0)
            value = value with { RecordStart = value.AvailableKey((int)Keys.F9) };
        if (value.RecordStop == 0)
            value = value with { RecordStop = value.AvailableKey((int)Keys.F10) };
        return value;
    }

    private int AvailableKey(int preferred) => Contains(preferred)
        ? Enumerable.Range((int)Keys.F1, 12).First(key => !Contains(key))
        : preferred;

    private static bool IsFunctionKey(int key) => key is >= (int)Keys.F1 and <= (int)Keys.F12;
}
