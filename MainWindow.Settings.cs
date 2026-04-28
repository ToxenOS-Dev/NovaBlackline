using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Linq;

namespace NovaBlackline;

public partial class MainWindow
{
    static readonly (string Label, string Id)[] TimeZoneOptions =
    [
        ("Local", ""),
        ("UTC", "UTC"),
        ("Copenhagen", "Europe/Copenhagen"),
        ("London", "Europe/London"),
        ("New York", "America/New_York"),
        ("Tokyo", "Asia/Tokyo"),
    ];

    static readonly ThemeProfile[] Themes =
    [
        new(Color.FromRgb(0, 0, 0),       Color.FromArgb(0xDD, 0, 0, 0),       Color.FromRgb(15, 15, 15),    210, 80, 220),
        new(Color.FromRgb(6, 10, 18),     Color.FromArgb(0xE5, 3, 8, 18),      Color.FromRgb(11, 18, 30),    220, 95, 230),
        new(Color.FromRgb(18, 18, 18),    Color.FromArgb(0xDD, 18, 18, 18),    Color.FromRgb(26, 26, 26),    190, 70, 210),
    ];

    void InitSettings()
    {
        _settings =
        [
            new("Display",  "Theme",            ["Blackline", "Midnight", "Graphite"],       0, ApplyTheme),
            new("Display",  "Accent Color",     ["Yellow", "White", "Cyan", "Red", "Green"], 0, ApplyAccent),
            new("Display",  "Primary Display Only", ["On", "Off"],                           0, ApplyPrimaryDisplayOnly),
            new("Time",     "Time Zone",        TimeZoneOptions.Select(t => t.Label).ToArray(), 0, ApplyTimeZone),
            new("Time",     "Clock Format",     ["24-hour", "12-hour"],                       0, ApplyClockFormat),
            new("Language", "Language",         ["English", "Danish"],                        0, ApplyLanguage),
            new("Controls", "Navigation Speed", ["Slow", "Normal", "Fast"],                  1, ApplyNavSpeed),
        ];
        _settingValues = _settings.Select(s => s.Default).ToArray();
        UpdateAccentResources();
        ApplyTheme(_settingValues[0]);
        ApplyPrimaryDisplayOnly(_settingValues[2]);
        ApplyLanguage(_settingValues[5]);
    }

    void OpenSettings()
    {
        _layer = Layer.Settings;
        _settingIndex = 0;
        SettingsOverlay.IsVisible = true;
        DrawSettingsRows();
    }

    void CloseSettings()
    {
        _layer = Layer.Tiles;
        SettingsOverlay.IsVisible = false;
        UpdateTopBar();
    }

    void DrawSettingsRows()
    {
        SettingsRows.Children.Clear();
        string? category = null;
        for (int i = 0; i < _settings.Length; i++)
        {
            bool sel     = i == _settingIndex;
            var  setting = _settings[i];
            int  valIdx  = _settingValues[i];

            if (category != setting.Category)
            {
                category = setting.Category;
                SettingsRows.Children.Add(new TextBlock
                {
                    Text       = TranslateCategory(category).ToUpper(),
                    FontSize   = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(Color.FromArgb(150, _accent.R, _accent.G, _accent.B)),
                    Margin     = new Thickness(4, SettingsRows.Children.Count == 0 ? 0 : 18, 0, 4),
                });
            }

            var row = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(sel ? (byte)45 : (byte)0, _accent.R, _accent.G, _accent.B)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(sel ? (byte)80 : (byte)0, _accent.R, _accent.G, _accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(20, 14),
                Margin          = new Thickness(0, 2),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            grid.Children.Add(new TextBlock
            {
                Text              = TranslateSetting(setting.Label),
                FontSize          = 15,
                Foreground        = sel ? new SolidColorBrush(_accent) : Brushes.White,
                FontWeight        = sel ? FontWeight.Bold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var valueRow = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                Spacing           = 14,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(valueRow, 1);

            byte arrowAlpha = sel ? (byte)255 : (byte)60;
            valueRow.Children.Add(new TextBlock
            {
                Text              = "◄",
                FontSize          = 13,
                Foreground        = new SolidColorBrush(Color.FromArgb(arrowAlpha, _accent.R, _accent.G, _accent.B)),
                VerticalAlignment = VerticalAlignment.Center,
            });
            valueRow.Children.Add(new TextBlock
            {
                Text              = setting.Options[valIdx],
                FontSize          = 14,
                Foreground        = Brushes.White,
                MinWidth          = 72,
                TextAlignment     = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            valueRow.Children.Add(new TextBlock
            {
                Text              = "►",
                FontSize          = 13,
                Foreground        = new SolidColorBrush(Color.FromArgb(arrowAlpha, _accent.R, _accent.G, _accent.B)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            grid.Children.Add(valueRow);
            row.Child = grid;
            SettingsRows.Children.Add(row);
        }
    }

    void ChangeSettingValue(int dir)
    {
        var setting = _settings[_settingIndex];
        int newVal  = Math.Clamp(_settingValues[_settingIndex] + dir, 0, setting.Options.Length - 1);
        _settingValues[_settingIndex] = newVal;
        setting.Apply(newVal);
        DrawSettingsRows();
    }

    void ApplyAccent(int idx)
    {
        _accent = AccentColors[idx];
        UpdateAccentResources();
        DrawTiles();
        UpdateTopBar();
        DrawSettingsRows();
        if (ShopOverlay.IsVisible)
        {
            DrawShopTabs();
            DrawShopContent();
        }
    }

    void ApplyTheme(int idx)
    {
        var theme = Themes[idx];
        _theme = theme;
        SetBrushResource("WindowBackgroundBrush", theme.WindowBackground);
        SetBrushResource("OverlayBackgroundBrush", theme.OverlayBackground);
        SetBrushResource("PanelBackgroundBrush", theme.PanelBackground);
        Background = new SolidColorBrush(theme.WindowBackground);
        UpdateBackground();
    }

    void ApplyTimeZone(int idx)
    {
        string id = TimeZoneOptions[idx].Id;
        _clockTimeZone = string.IsNullOrEmpty(id) ? TimeZoneInfo.Local : FindTimeZone(id);
        UpdateClock();
    }

    void ApplyClockFormat(int idx)
    {
        _clockFormat = idx == 0 ? "HH:mm" : "h:mm tt";
        UpdateClock();
    }

    void ApplyLanguage(int idx)
    {
        _languageIndex = idx;
        SettingsTitleText.Text = T("SETTINGS", "INDSTILLINGER");
        SettingsHintText.Text  = T("↑ ↓  select        ← →  change        ESC  close",
                                   "↑ ↓  vælg        ← →  skift        ESC  luk");
        StoreText.Text         = T("STORE", "BUTIK");
        BottomHintText.Text    = T("←  →   navigate        ENTER   launch        ESC   quit",
                                   "←  →   naviger        ENTER   start        ESC   afslut");
        StartTagline.Text      = T("YOUR GAMES. YOUR WAY.", "DINE SPIL. PÅ DIN MÅDE.");
        DrawSettingsRows();
        if (ShopOverlay.IsVisible) UpdateShopFooter();
    }

    void UpdateAccentResources()
    {
        SetBrushResource("AccentBrush", _accent);
        SetBrushResource("AccentLineBrush", WithAlpha(0x55));
        SetBrushResource("AccentButtonBackgroundBrush", WithAlpha(0x1A));
        SetBrushResource("AccentButtonBorderBrush", WithAlpha(0x44));
        SetBrushResource("AccentShopBorderBrush", WithAlpha(0x33));
        SetBrushResource("AccentDividerBrush", WithAlpha(0x22));
        SetBrushResource("AccentSettingsBorderBrush", WithAlpha(0x55));
    }

    Color WithAlpha(byte alpha) => Color.FromArgb(alpha, _accent.R, _accent.G, _accent.B);

    void SetBrushResource(string key, Color color)
    {
        if (Resources.TryGetResource(key, null, out var resource) && resource is SolidColorBrush brush)
            brush.Color = color;
        else
            Resources[key] = new SolidColorBrush(color);
    }

    void ApplyNavSpeed(int idx)
    {
        int[] delays = [420, 280, 150];
        _navRepeatMs = delays[idx];
    }

    static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch { return TimeZoneInfo.Local; }
    }

    string T(string english, string danish) => _languageIndex == 1 ? danish : english;

    string TranslateCategory(string category) => category switch
    {
        "Display"  => T("Display", "Skærm"),
        "Time"     => T("Time", "Tid"),
        "Language" => T("Language", "Sprog"),
        "Controls" => T("Controls", "Styring"),
        _ => category,
    };

    string TranslateSetting(string label) => label switch
    {
        "Theme"            => T("Theme", "Tema"),
        "Accent Color"     => T("Accent Color", "Accentfarve"),
        "Primary Display Only" => T("Primary Display Only", "Kun primær skærm"),
        "Time Zone"        => T("Time Zone", "Tidszone"),
        "Clock Format"     => T("Clock Format", "Urformat"),
        "Language"         => T("Language", "Sprog"),
        "Navigation Speed" => T("Navigation Speed", "Navigationshastighed"),
        _ => label,
    };
}
