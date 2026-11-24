using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Clickold;

internal enum AppTheme { Light, Dark, Meme }

internal sealed class ThemeMenuColors(ThemeColors palette) : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => palette.Surface;
    public override Color MenuBorder => palette.Border;
    public override Color MenuItemBorder => palette.Border;
    public override Color MenuItemSelected => palette.Hover;
}

internal sealed record ThemeColors(
    Color Background, Color Surface, Color Text, Color Muted, Color Border, Color Hover)
{
    public static Color Chair { get; } = ColorTranslator.FromHtml("#8C2443");
    public static Color ChairSecondary { get; } = ColorTranslator.FromHtml("#63263C");
    public static Color ChairHover { get; } = ColorTranslator.FromHtml("#A23355");
    public static Color ChairPressed { get; } = ColorTranslator.FromHtml("#6C1B37");
    public static Color ChairText { get; } = ColorTranslator.FromHtml("#FFF5F2");

    public static ThemeColors For(AppTheme theme) => theme switch
    {
        AppTheme.Light => Create("#F5F6F3", "#FFFFFF", "#182422", "#4B5A55", "#C8D2CD",
            "#E9EEEB"),
        AppTheme.Dark => Create("#181C20", "#22282E", "#F2F5F6", "#BAC4CB", "#46515B",
            "#313B44"),
        _ => Create("#221B27", "#302633", "#FCF3ED", "#CFBECF", "#624B61",
            "#413343")
    };

    private static ThemeColors Create(string background, string surface, string text, string muted,
        string border, string hover) =>
        new(Parse(background), Parse(surface), Parse(text), Parse(muted), Parse(border), Parse(hover));

    private static Color Parse(string value) => ColorTranslator.FromHtml(value);
}

internal static class Appearance
{
    private static readonly string ThemePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Clickold", "theme.txt");

    public static AppTheme LoadTheme()
    {
        try
        {
            var saved = File.ReadAllText(ThemePath).Trim();
            if (Enum.TryParse<AppTheme>(saved, out var theme) && Enum.IsDefined(theme))
                return theme;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        return AppTheme.Meme;
    }

    public static void SaveTheme(AppTheme theme)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ThemePath)!);
        File.WriteAllText(ThemePath, theme.ToString());
    }

    public static Bitmap LoadImage(string name)
    {
        using var stream = OpenAsset(name);
        using var image = Image.FromStream(stream);
        return new Bitmap(image);
    }

    public static Icon LoadIcon()
    {
        using var stream = OpenAsset("clickold.ico");
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }

    private static Stream OpenAsset(string name) =>
        typeof(Appearance).Assembly.GetManifestResourceStream($"Clickold.Art.{name}")
        ?? throw new FileNotFoundException($"Missing embedded asset {name}.");

    public static void ApplyCaption(nint window, ThemeColors colors)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;
        const int captionColor = 35;
        const int textColor = 36;
        int background = ColorTranslator.ToWin32(colors.Background);
        int foreground = ColorTranslator.ToWin32(colors.Text);
        DwmSetWindowAttribute(window, captionColor, ref background, sizeof(int));
        DwmSetWindowAttribute(window, textColor, ref foreground, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}

internal sealed class ThemeButton : Button
{
    public Color DisabledText { get; set; }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled)
        {
            base.OnPaint(e);
            return;
        }

        e.Graphics.Clear(BackColor);
        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, FlatAppearance.BorderColor, ButtonBorderStyle.Solid);
        TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, DisabledText,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
    }
}

internal sealed class IllustrationPanel : Control
{
    private Bitmap? illustration;

    public IllustrationPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        AccessibleName = UiText.Get(TextId.Illustration);
        AccessibleRole = AccessibleRole.Graphic;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (ClientSize.Width == 0 || ClientSize.Height == 0)
            return;

        illustration ??= Appearance.LoadImage("meme.jpg");
        float scale = Math.Max((float)Width / illustration.Width, (float)Height / illustration.Height);
        float width = illustration.Width * scale;
        float height = illustration.Height * scale;
        var frame = new RectangleF(0, 0, Width, Height);
        using var path = new GraphicsPath();
        float diameter = Math.Min(24f * DeviceDpi / 96, Math.Min(Width, Height));
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(frame.Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(frame.Width - diameter, frame.Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, frame.Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var graphicsState = e.Graphics.Save();
        e.Graphics.SetClip(path, CombineMode.Intersect);
        e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(illustration, (Width - width) / 2, (Height - height) / 2, width, height);
        e.Graphics.Restore(graphicsState);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            illustration?.Dispose();
        base.Dispose(disposing);
    }
}
