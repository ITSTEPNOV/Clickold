using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using static Clickold.UiText;

namespace Clickold;

internal sealed class MainForm : Form
{
    private const int ControlsWidth = 528;
    private const int ArtworkWidth = 292;
    private readonly ThemeButton record = new() { Text = Get(TextId.Record) };
    private readonly ThemeButton save = new() { Text = Get(TextId.Save) };
    private readonly ThemeButton open = new() { Text = Get(TextId.Open) };
    private readonly ThemeButton start = new() { Text = Get(TextId.Start) };
    private readonly ThemeButton pause = new() { Text = Get(TextId.Pause) };
    private readonly ThemeButton stop = new() { Text = Get(TextId.Stop) };
    private readonly NumericUpDown repetitions = new() { Minimum = 1, Maximum = 1_000_000, Value = 1 };
    private readonly NumericUpDown speed = new()
    {
        Minimum = 0.10m, Maximum = 10, Value = 1, DecimalPlaces = 2, Increment = 0.25m
    };
    private readonly CheckBox loop = new() { Text = Get(TextId.Loop), AutoSize = true };
    private readonly ComboBox startKey = HotkeyPicker();
    private readonly ComboBox stopKey = HotkeyPicker();
    private readonly ComboBox pauseKey = HotkeyPicker();
    private readonly ComboBox recordStartKey = HotkeyPicker();
    private readonly ComboBox recordStopKey = HotkeyPicker();
    private readonly ComboBox[] hotkeyPickers;
    private readonly ThemeButton musicButton = new() { Text = "♪", Size = new Size(38, 31) };
    private readonly ToolTip musicTip = new();
    private readonly Font musicFont = new("Segoe UI Symbol", 14, FontStyle.Bold);
    private readonly ThemeButton themeButton = new() { Size = new Size(116, 31) };
    private readonly ContextMenuStrip themeMenu = new() { ShowCheckMargin = false, ShowImageMargin = false };
    private readonly IllustrationPanel illustration = new() { TabStop = false };
    private readonly ColumnStyle illustrationColumn = new(SizeType.Absolute);
    private readonly Font titleFont = new("Segoe UI", 17, FontStyle.Bold);
    private readonly Bitmap logo = Appearance.LoadImage("logo.png");
    private readonly Icon appIcon = Appearance.LoadIcon();
    private readonly Label macroInfo = new() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label counter = new() { AutoSize = true };
    private readonly Label status = new() { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly System.Windows.Forms.Timer recordTimer = new() { Interval = 120 };
    private readonly HashSet<int> pressedHotkeys = [];
    private AppTheme theme = Appearance.LoadTheme();
    private ThemeColors palette = ThemeColors.For(AppTheme.Meme);
    private Hotkeys hotkeys = Hotkeys.Load();
    private WindowsInput? input;
    private Macro? macro;
    private Recorder? recorder;
    private Playback? playback;
    private MusicPlayback? musicPlayback;
    private CancellationTokenSource? playbackCancellation;
    private string? macroPath;
    private bool restoreWindow;
    private bool closeRequested;
    private bool updatingHotkeys;
    private bool fileDialogOpen;
    private long completed;
    private int target;
    private bool infinite;

    public MainForm()
    {
        AccessibleName = "Clickold";
        Icon = appIcon;
        ShowIcon = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ClientSize = new Size(ControlsWidth + (theme == AppTheme.Meme ? ArtworkWidth : 0), 382);
        hotkeyPickers = [startKey, stopKey, pauseKey, recordStartKey, recordStopKey];

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(illustrationColumn);
        Controls.Add(root);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            Margin = Padding.Empty,
            Padding = new Padding(20, 12, 20, 12)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(layout, 0, 0);
        illustration.Dock = DockStyle.Fill;
        illustration.Margin = new Padding(0, 16, 16, 16);
        root.Controls.Add(illustration, 1, 0);

        AddRow(CreateHeader(), 56);
        AddRow(macroInfo, 34);
        AddRow(ButtonRow(record, save, open), 44);
        AddRow(Flow(LabelFor(Get(TextId.Repeats)), repetitions, LabelFor($"{Get(TextId.Speed)} ×"), speed, loop), 42);
        AddRow(ButtonRow(start, pause, stop), 44);
        AddRow(Flow(HotkeyGroup(Get(TextId.RecordGroup)), LabelFor(Get(TextId.Start)), recordStartKey,
            LabelFor(Get(TextId.Stop)), recordStopKey), 36);
        AddRow(Flow(HotkeyGroup(Get(TextId.PlaybackGroup)), LabelFor(Get(TextId.Start)), startKey,
            LabelFor(Get(TextId.Stop)), stopKey, LabelFor(Get(TextId.Pause)), pauseKey), 36);
        AddRow(Flow(counter), 29);
        AddRow(status, 34);

        repetitions.Width = 76;
        speed.Width = 65;
        macroInfo.Margin = new Padding(0, 4, 0, 4);
        macroInfo.Padding = new Padding(10, 0, 6, 0);
        loop.FlatStyle = FlatStyle.Flat;
        loop.Margin = new Padding(12, 7, 0, 0);
        status.AccessibleName = Get(TextId.Status);
        repetitions.AccessibleName = Get(TextId.RepeatCount);
        speed.AccessibleName = Get(TextId.Speed);
        startKey.AccessibleName = Get(TextId.PlaybackStartKey);
        stopKey.AccessibleName = Get(TextId.PlaybackStopKey);
        pauseKey.AccessibleName = Get(TextId.PlaybackPauseKey);
        recordStartKey.AccessibleName = Get(TextId.RecordStartKey);
        recordStopKey.AccessibleName = Get(TextId.RecordStopKey);
        foreach (var choice in new[] { AppTheme.Meme, AppTheme.Light, AppTheme.Dark })
        {
            var item = new ToolStripMenuItem(ThemeName(choice)) { Tag = choice };
            item.Click += (_, _) => ChangeTheme(choice);
            themeMenu.Items.Add(item);
        }
        themeButton.Click += (_, _) => themeMenu.Show(themeButton, new Point(0, themeButton.Height + 3));
        themeButton.ContextMenuStrip = themeMenu;
        musicButton.Font = musicFont;
        musicButton.Click += (_, _) => ToggleMusic();
        UpdateMusicButton();

        foreach (var button in new[] { record, save, open, start, pause, stop, musicButton, themeButton })
        {
            button.FlatStyle = FlatStyle.Flat;
            button.UseVisualStyleBackColor = false;
            button.EnabledChanged += (_, _) => StyleButton(button);
        }
        foreach (var picker in hotkeyPickers)
            picker.DrawItem += DrawPickerItem;

        record.Click += (_, _) => StartRecording();
        save.Click += (_, _) => SaveMacro();
        open.Click += (_, _) => OpenMacro();
        start.Click += (_, _) => StartPlayback();
        pause.Click += (_, _) => TogglePause();
        stop.Click += (_, _) =>
        {
            if (recorder is not null)
                StopRecording();
            else
                StopPlayback();
        };
        loop.CheckedChanged += (_, _) => UpdateControls();
        recordTimer.Tick += (_, _) => UpdateMacroInfo();
        ShowHotkeys();
        foreach (var picker in hotkeyPickers)
            picker.SelectedIndexChanged += (_, _) => ChangeHotkeys();

        SetStatus(Get(TextId.Ready));
        ApplyTheme();
        UpdateControls();

        void AddRow(Control control, int height)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            control.Dock = DockStyle.Fill;
            layout.Controls.Add(control, 0, layout.RowCount++);
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ResizeForTheme();
        Appearance.ApplyCaption(Handle, palette);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        BeginInvoke(ResizeForTheme);
    }

    private Control CreateHeader()
    {
        var header = new TableLayoutPanel { ColumnCount = 5, RowCount = 1, Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 53));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var logoBox = new PictureBox
        {
            Image = logo, SizeMode = PictureBoxSizeMode.Zoom, Dock = DockStyle.Fill,
            Margin = new Padding(0, 2, 8, 6)
        };
        var title = new Label
        {
            Text = "Clickold", Font = titleFont, TextAlign = ContentAlignment.MiddleLeft,
            Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4)
        };
        var themeLabel = LabelFor(Get(TextId.Theme));
        themeLabel.Anchor = AnchorStyles.Left;
        themeLabel.Margin = new Padding(7, 0, 8, 4);
        musicButton.Anchor = AnchorStyles.Left;
        musicButton.Margin = new Padding(0, 0, 10, 4);
        themeButton.Anchor = AnchorStyles.Left;
        themeButton.Margin = new Padding(0, 0, 0, 4);
        header.Controls.Add(logoBox, 0, 0);
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(musicButton, 2, 0);
        header.Controls.Add(themeLabel, 3, 0);
        header.Controls.Add(themeButton, 4, 0);
        return header;
    }

    private static string ThemeName(AppTheme choice) => Get(choice switch
    {
        AppTheme.Light => TextId.ThemeLight,
        AppTheme.Dark => TextId.ThemeDark,
        _ => TextId.ThemeStandard
    });

    private void ChangeTheme(AppTheme choice)
    {
        if (theme == choice)
            return;
        theme = choice;
        ApplyTheme();
        ResizeForTheme();
        Appearance.ApplyCaption(Handle, palette);
        try
        {
            Appearance.SaveTheme(theme);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetStatus(Get(TextId.ThemeTemporary));
        }
    }

    private void ApplyTheme()
    {
        palette = ThemeColors.For(theme);
        BackColor = palette.Background;
        ForeColor = palette.Text;
        macroInfo.BackColor = palette.Surface;
        macroInfo.ForeColor = palette.Muted;
        status.ForeColor = palette.Muted;
        counter.ForeColor = palette.Text;
        foreach (var control in new Control[] { startKey, stopKey, pauseKey, recordStartKey, recordStopKey, repetitions, speed })
        {
            control.BackColor = palette.Surface;
            control.ForeColor = palette.Text;
        }
        foreach (var button in new[] { record, save, open, start, pause, stop, musicButton, themeButton })
            StyleButton(button);
        themeButton.Text = $"{ThemeName(theme)}  ▾";
        themeButton.AccessibleName = $"{Get(TextId.Theme)} — {ThemeName(theme)}";
        themeMenu.BackColor = palette.Surface;
        themeMenu.Renderer = new ToolStripProfessionalRenderer(new ThemeMenuColors(palette)) { RoundedEdges = false };
        foreach (ToolStripMenuItem item in themeMenu.Items)
        {
            item.ForeColor = palette.Text;
            var choice = (AppTheme)item.Tag!;
            item.Checked = choice == theme;
            item.Text = item.Checked ? $"{ThemeName(choice)}  ✓" : ThemeName(choice);
        }
        illustration.Visible = theme == AppTheme.Meme;
    }

    private void ResizeForTheme()
    {
        var screen = Screen.FromControl(this).WorkingArea;
        int controls = (ControlsWidth * DeviceDpi + 48) / 96;
        int desiredArtwork = theme == AppTheme.Meme ? (ArtworkWidth * DeviceDpi + 48) / 96 : 0;
        int frame = Width - ClientSize.Width;
        int available = Math.Max(0, screen.Width - controls - frame);
        int artwork = Math.Min(desiredArtwork, available);
        illustrationColumn.Width = artwork;
        ClientSize = new Size(controls + artwork, ClientSize.Height);
        if (Visible && WindowState == FormWindowState.Normal && Right > screen.Right)
            Left = Math.Max(screen.Left, screen.Right - Width);
    }

    private void StyleButton(ThemeButton button)
    {
        if (button == musicButton)
        {
            bool playing = musicPlayback is not null;
            button.BackColor = playing ? ThemeColors.ChairPressed : palette.Surface;
            button.ForeColor = theme == AppTheme.Light && !playing ? Color.FromArgb(170, 34, 73)
                : Color.FromArgb(255, 142, 160);
            button.DisabledText = palette.Muted;
            button.FlatAppearance.BorderColor = playing ? ThemeColors.ChairHover : palette.Border;
            button.FlatAppearance.MouseOverBackColor = playing ? ThemeColors.Chair : palette.Hover;
            button.FlatAppearance.MouseDownBackColor = palette.Border;
            return;
        }

        bool primary = button.Enabled && (button == start || button == record);
        button.BackColor = primary ? ThemeColors.Chair
            : button.Enabled ? ThemeColors.ChairSecondary : palette.Background;
        button.ForeColor = ThemeColors.ChairText;
        button.DisabledText = palette.Muted;
        button.FlatAppearance.BorderColor = button.Enabled ? button.BackColor : palette.Border;
        button.FlatAppearance.MouseOverBackColor = ThemeColors.ChairHover;
        button.FlatAppearance.MouseDownBackColor = ThemeColors.ChairPressed;
    }

    private void DrawPickerItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox picker || e.Index < 0)
            return;
        bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
        bool disabled = (e.State & DrawItemState.Disabled) != 0 || !picker.Enabled;
        using var background = new SolidBrush(selected ? ThemeColors.Chair : palette.Surface);
        e.Graphics.FillRectangle(background, e.Bounds);
        var textBounds = Rectangle.Inflate(e.Bounds, -3, 0);
        TextRenderer.DrawText(e.Graphics, picker.GetItemText(picker.Items[e.Index]), picker.Font, textBounds,
            selected ? ThemeColors.ChairText : disabled ? palette.Muted : palette.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        e.DrawFocusRectangle();
    }

    private void ToggleMusic()
    {
        try
        {
            if (musicPlayback is { } playing)
            {
                playing.Stop();
                FinishMusic(playing, null);
                return;
            }

            var player = new MusicPlayback();
            musicPlayback = player;
            player.Stopped += (_, e) => FinishMusic(player, e.Exception);
            player.Play();
            UpdateMusicButton();
        }
        catch (Exception error) when (error is NAudio.MmException or IOException or InvalidDataException
            or InvalidOperationException or ExternalException)
        {
            FinishMusic(musicPlayback, error);
        }
    }

    private void FinishMusic(MusicPlayback? player, Exception? error)
    {
        if (IsDisposed || player != musicPlayback)
            return;
        musicPlayback = null;
        try
        {
            player?.Dispose();
        }
        catch (Exception failure) when (failure is NAudio.MmException or ExternalException)
        {
            error ??= failure;
        }
        UpdateMusicButton();
        if (error is not null)
            SetStatus(Get(TextId.MusicFailed));
    }

    private void UpdateMusicButton()
    {
        string action = Get(musicPlayback is null ? TextId.MusicOn : TextId.MusicOff);
        musicButton.AccessibleName = action;
        musicTip.SetToolTip(musicButton, action);
        StyleButton(musicButton);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            input = new WindowsInput(HandleKeyboard, item => recorder?.Mouse(item, Handle));
        }
        catch (Win32Exception)
        {
            SetStatus(Get(TextId.InputUnavailable));
        }
        UpdateControls();
    }

    private bool HandleKeyboard(WindowsInput.KeyboardEvent item)
    {
        if (item.Up && pressedHotkeys.Remove(item.Key))
            return true;
        if (pressedHotkeys.Contains(item.Key))
            return true;
        if (!fileDialogOpen && hotkeys.Contains(item.Key))
        {
            if (!item.Up)
            {
                pressedHotkeys.Add(item.Key);
                BeginInvoke(() => ExecuteHotkey(item.Key));
                return true;
            }
            return false;
        }
        recorder?.Keyboard(item, WindowsInput.IsForeground(Handle));
        return false;
    }

    private void ExecuteHotkey(int key)
    {
        if (fileDialogOpen || closeRequested)
            return;
        if (key == hotkeys.Start)
            StartPlayback();
        else if (key == hotkeys.Stop)
            StopPlayback();
        else if (key == hotkeys.Pause)
            TogglePause();
        else if (key == hotkeys.RecordStart)
            StartRecording();
        else if (key == hotkeys.RecordStop)
            StopRecording();
    }

    private void StartRecording()
    {
        if (input is null || recorder is not null || playback is not null)
            return;

        MinimizeIfFocused();
        recorder = new Recorder();
        input.CaptureMouse = true;
        recordTimer.Start();
        SetStatus(Format(TextId.RecordingStop, (Keys)hotkeys.RecordStop));
        UpdateControls();
    }

    private async void StartPlayback()
    {
        if (input is null || macro is null || recorder is not null || playback is not null)
            return;

        MinimizeIfFocused();
        using var cancellation = new CancellationTokenSource();
        playbackCancellation = cancellation;
        playback = new Playback();
        completed = 0;
        target = (int)repetitions.Value;
        infinite = loop.Checked;
        SetStatus(Format(TextId.PlayingStop, (Keys)hotkeys.Stop));
        UpdateControls();
        try
        {
            await playback.Run(macro, (double)speed.Value, target, infinite, restoreWindow, count =>
            {
                completed = count;
                UpdateCounter();
            }, cancellation.Token);
            SetStatus(Get(TextId.PlaybackComplete));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            SetStatus(Get(TextId.PlaybackStopped));
        }
        catch (Win32Exception error)
        {
            SetStatus(error.Message);
        }
        finally
        {
            playback = null;
            playbackCancellation = null;
            if (closeRequested)
                BeginInvoke(Close);
            else
            {
                RestoreWindow();
                UpdateControls();
            }
        }
    }

    private void TogglePause()
    {
        if (playback is null || playbackCancellation!.IsCancellationRequested)
            return;
        playback.TogglePause();
        SetStatus(playback.IsPaused
            ? Format(TextId.PausedResume, (Keys)hotkeys.Pause)
            : Format(TextId.PlayingStop, (Keys)hotkeys.Stop));
        UpdateControls();
    }

    private void StopRecording()
    {
        if (recorder is null)
            return;

        var recorded = recorder.Finish();
        recorder = null;
        input!.CaptureMouse = false;
        recordTimer.Stop();
        if (recorded.Events.Length > 0)
        {
            SetMacro(recorded, null);
            SetStatus(Get(TextId.RecordingReady));
        }
        else
        {
            SetStatus(Get(TextId.RecordingEmpty));
        }
        RestoreWindow();
        UpdateControls();
    }

    private void StopPlayback()
    {
        if (playbackCancellation is { IsCancellationRequested: false } cancellation)
        {
            SetStatus(Get(TextId.Stopping));
            cancellation.Cancel();
            UpdateControls();
        }
    }

    private void MinimizeIfFocused()
    {
        restoreWindow = WindowsInput.IsForeground(Handle);
        if (restoreWindow)
            WindowState = FormWindowState.Minimized;
    }

    private void RestoreWindow()
    {
        if (restoreWindow && WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        restoreWindow = false;
    }

    private void OpenMacro()
    {
        using var dialog = new OpenFileDialog { Filter = Get(TextId.MacroFilter), RestoreDirectory = true };
        WithFileDialog(dialog, path =>
        {
            SetMacro(Macro.Load(path), path);
            SetStatus(Get(TextId.MacroOpened));
        });
    }

    private void SaveMacro()
    {
        if (macro is not { } current)
            return;

        using var dialog = new SaveFileDialog
        {
            Filter = Get(TextId.MacroFilter),
            FileName = Path.GetFileName(macroPath) ?? $"{Get(TextId.DefaultMacro)}.json",
            InitialDirectory = Path.GetDirectoryName(macroPath) ?? "",
            RestoreDirectory = true
        };
        WithFileDialog(dialog, path =>
        {
            current.Save(path);
            macroPath = path;
            SetStatus(Get(TextId.MacroSaved));
        });
    }

    private void WithFileDialog(FileDialog dialog, Action<string> action)
    {
        fileDialogOpen = true;
        try
        {
            if (dialog.ShowDialog(this) == DialogResult.OK)
                action(dialog.FileName);
        }
        catch (InvalidDataException error)
        {
            SetStatus(error.Message);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            SetStatus(Get(dialog is SaveFileDialog ? TextId.MacroWriteFailed : TextId.MacroReadFailed));
        }
        finally
        {
            fileDialogOpen = false;
            UpdateControls();
        }
    }

    private void SetMacro(Macro value, string? path)
    {
        macro = value;
        macroPath = path;
        completed = target = 0;
        infinite = false;
    }

    private void UpdateControls()
    {
        bool idle = recorder is null && playback is null;
        bool canStop = recorder is not null || playbackCancellation is { IsCancellationRequested: false };
        record.Enabled = idle && input is not null;
        save.Enabled = idle && macro is not null;
        open.Enabled = idle;
        start.Enabled = idle && macro is not null && input is not null;
        stop.Enabled = canStop;
        pause.Enabled = playback is not null && canStop;
        pause.Text = Get(playback is { IsPaused: true } ? TextId.Resume : TextId.Pause);
        repetitions.Enabled = idle && !loop.Checked;
        speed.Enabled = loop.Enabled = idle;
        foreach (var picker in hotkeyPickers)
            picker.Enabled = idle;
        UpdateMacroInfo();
        UpdateCounter();
    }

    private void UpdateMacroInfo()
    {
        if (recorder is not null)
            macroInfo.Text = $"{Get(TextId.Recording)}   ·   {Format(TextId.Events, recorder.Count)}";
        else if (macro is not null)
            macroInfo.Text = $"{Path.GetFileName(macroPath) ?? Get(TextId.NewRecording)}   ·   {Format(TextId.Events, macro.Events.Length)}";
        else
            macroInfo.Text = Get(TextId.NoMacro);
    }

    private void UpdateCounter()
    {
        var count = infinite ? $"{completed} / ∞" : target > 0 ? $"{completed} / {target}" : $"{completed}";
        counter.Text = Format(TextId.Completed, count);
    }

    private void SetStatus(string message) => status.Text = message;

    private void ChangeHotkeys()
    {
        if (updatingHotkeys)
            return;
        var chosen = new Hotkeys((int)Keys.F1 + startKey.SelectedIndex,
            (int)Keys.F1 + stopKey.SelectedIndex, (int)Keys.F1 + pauseKey.SelectedIndex,
            (int)Keys.F1 + recordStartKey.SelectedIndex, (int)Keys.F1 + recordStopKey.SelectedIndex);
        if (!chosen.IsValid)
        {
            ShowHotkeys();
            SetStatus(Get(TextId.DifferentKeys));
            return;
        }
        hotkeys = chosen;
        try
        {
            hotkeys.Save();
            SetStatus(Get(TextId.HotkeysSaved));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetStatus(Get(TextId.HotkeysTemporary));
        }
    }

    private void ShowHotkeys()
    {
        updatingHotkeys = true;
        startKey.SelectedIndex = hotkeys.Start - (int)Keys.F1;
        stopKey.SelectedIndex = hotkeys.Stop - (int)Keys.F1;
        pauseKey.SelectedIndex = hotkeys.Pause - (int)Keys.F1;
        recordStartKey.SelectedIndex = hotkeys.RecordStart - (int)Keys.F1;
        recordStopKey.SelectedIndex = hotkeys.RecordStop - (int)Keys.F1;
        updatingHotkeys = false;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (playback is not null)
        {
            e.Cancel = true;
            closeRequested = true;
            StopPlayback();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var player = musicPlayback;
            musicPlayback = null;
            try
            {
                player?.Dispose();
            }
            catch (Exception error) when (error is NAudio.MmException or ExternalException) { }
            recordTimer.Dispose();
            musicTip.Dispose();
            themeMenu.Dispose();
            input?.Dispose();
        }
        base.Dispose(disposing);
        if (disposing)
        {
            logo.Dispose();
            appIcon.Dispose();
            titleFont.Dispose();
            musicFont.Dispose();
        }
    }

    private static TableLayoutPanel ButtonRow(params Button[] buttons)
    {
        var row = new TableLayoutPanel { ColumnCount = buttons.Length, RowCount = 1, Margin = Padding.Empty };
        for (var index = 0; index < buttons.Length; index++)
        {
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / buttons.Length));
            var button = buttons[index];
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(0, 2, index == buttons.Length - 1 ? 0 : 8, 4);
            row.Controls.Add(button, index, 0);
        }
        return row;
    }

    private static FlowLayoutPanel Flow(params Control[] controls)
    {
        var row = new FlowLayoutPanel { Margin = Padding.Empty, Padding = new Padding(0, 6, 0, 0), WrapContents = false };
        row.Controls.AddRange(controls);
        return row;
    }

    private static Label LabelFor(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(0, 7, 5, 0) };

    private static Label HotkeyGroup(string text) =>
        new() { Text = text, Width = 76, Margin = new Padding(0, 7, 5, 0) };

    private static ComboBox HotkeyPicker()
    {
        var picker = Picker(66);
        picker.Items.AddRange(Enumerable.Range(1, 12).Select(number => $"F{number}").ToArray());
        return picker;
    }

    private static ComboBox Picker(int width) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        DrawMode = DrawMode.OwnerDrawFixed,
        FlatStyle = FlatStyle.Flat,
        ItemHeight = 20,
        Width = width,
        Margin = new Padding(3, 3, 14, 3)
    };
}
