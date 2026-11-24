using System.Globalization;
using System.Resources;

namespace Clickold;

internal static class UiText
{
    private static readonly ResourceManager Resources = new("Clickold.Resources.Strings", typeof(UiText).Assembly);

    public static string Get(TextId id) => Resources.GetString(id.ToString(), ResourceCulture(CultureInfo.CurrentUICulture))
        ?? throw new MissingManifestResourceException($"Missing Clickold text: {id}");

    public static string Format(TextId id, object value) => string.Format(CultureInfo.CurrentCulture, Get(id), value);

    private static CultureInfo ResourceCulture(CultureInfo systemLanguage) => systemLanguage.TwoLetterISOLanguageName switch
    {
        "ru" => CultureInfo.GetCultureInfo("ru"),
        "zh" => CultureInfo.GetCultureInfo("zh-Hans"),
        _ => CultureInfo.InvariantCulture
    };
}

internal enum TextId
{
    Record,
    Save,
    Open,
    Start,
    Pause,
    Stop,
    Resume,
    Loop,
    Repeats,
    Speed,
    RecordGroup,
    PlaybackGroup,
    Status,
    RepeatCount,
    PlaybackStartKey,
    PlaybackStopKey,
    PlaybackPauseKey,
    RecordStartKey,
    RecordStopKey,
    Ready,
    Theme,
    ThemeLight,
    ThemeDark,
    ThemeStandard,
    ThemeTemporary,
    MusicFailed,
    MusicOn,
    MusicOff,
    InputUnavailable,
    InputRejected,
    RecordingStop,
    PlayingStop,
    PlaybackComplete,
    PlaybackStopped,
    PausedResume,
    RecordingReady,
    RecordingEmpty,
    Stopping,
    MacroOpened,
    MacroSaved,
    MacroFilter,
    DefaultMacro,
    MacroReadFailed,
    MacroWriteFailed,
    MacroUnsupported,
    MacroInvalidEvent,
    Recording,
    NewRecording,
    Events,
    NoMacro,
    Completed,
    DifferentKeys,
    HotkeysSaved,
    HotkeysTemporary,
    Illustration
}
