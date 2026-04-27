using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using IOPath      = System.IO.Path;
using IOFile      = System.IO.File;
using IODirectory = System.IO.Directory;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NovaBlackline;

record LaunchItem(string Name, string Icon, string Description, string Command, Color AccentColor, string? WallpaperPath = null);

enum Layer { Tiles, TopBar }

public partial class MainWindow : Window
{
    static readonly Color Yellow = Color.FromRgb(255, 215, 0);

    static readonly LaunchItem[] Items = BuildItems();

    static readonly string WallpaperDir =
        IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpapers");

    static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    static readonly double[] TileSizes   = [200, 155, 120, 90];
    static readonly double[] TileOffsets = [0,   265, 430, 560];
    static readonly double[] TileOpacity = [1.0, 0.6, 0.35, 0.18];

    int   _current;
    Layer _layer       = Layer.Tiles;
    int   _topBarIndex = 0;
    Timer? _clockTimer;
    readonly Dictionary<int, Bitmap?> _wallpapers = new();

    // ── Item discovery ────────────────────────────────────────────────────────

    static LaunchItem[] BuildItems()
    {
        var items = new List<LaunchItem>();
        items.AddRange(DiscoverSteamGames());
        items.Add(new("Terminal", ">", "Command line",    ResolveTerminal(),                                Color.FromRgb(30,  30,  30)));
        items.Add(new("Spotify",  "S", "Music streaming", ResolveApp("spotify",  "com.spotify.Client"),    Color.FromRgb(29,  185, 84)));
        items.Add(new("Discord",  "@", "Voice and chat",  ResolveApp("discord",  "com.discordapp.Discord"), Color.FromRgb(88,  101, 242)));
        return items.ToArray();
    }

    static string? FindBinary(string cmd)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] dirs =
        [
            "/usr/bin", "/usr/local/bin", "/bin",
            IOPath.Combine(home, ".local", "bin"),
        ];
        return dirs.Select(d => IOPath.Combine(d, cmd)).FirstOrDefault(IOFile.Exists);
    }

    static string ResolveApp(string nativeCmd, string flatpakId)
    {
        var path = FindBinary(nativeCmd);
        if (path != null) return path;
        if (IOFile.Exists("/usr/bin/flatpak")) return $"flatpak run {flatpakId}";
        return nativeCmd;
    }

    static string ResolveTerminal()
    {
        string[] candidates = ["ptyxis", "kitty", "alacritty", "wezterm", "gnome-terminal", "kgx", "konsole", "xterm"];
        return candidates.Select(FindBinary).FirstOrDefault(p => p != null) ?? "xterm";
    }

    static IEnumerable<LaunchItem> DiscoverSteamGames()
    {
        string? steamRoot = FindSteamRoot();
        if (steamRoot == null) yield break;

        var seen = new HashSet<string>();

        foreach (string libraryPath in GetSteamLibraryPaths(steamRoot))
        {
            if (!IODirectory.Exists(libraryPath)) continue;

            foreach (string manifest in IODirectory.GetFiles(libraryPath, "appmanifest_*.acf"))
            {
                if (!TryParseManifest(manifest, out string? appId, out string? name)) continue;
                if (!seen.Add(appId!)) continue;
                if (!IsGame(name!)) continue;

                yield return new LaunchItem(
                    Name:        name!,
                    Icon:        name![0].ToString().ToUpper(),
                    Description: name!,
                    Command:     $"xdg-open steam://rungameid/{appId}",
                    AccentColor: AppIdToColor(int.Parse(appId!)),
                    WallpaperPath: FindSteamArt(appId!));
            }
        }
    }

    static string? FindSteamRoot()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] candidates =
        [
            IOPath.Combine(home, ".local", "share", "Steam"),
            IOPath.Combine(home, ".steam", "steam"),
        ];
        return candidates.FirstOrDefault(
            p => IODirectory.Exists(IOPath.Combine(p, "steamapps")));
    }

    static List<string> GetSteamLibraryPaths(string steamRoot)
    {
        var paths = new List<string> { IOPath.Combine(steamRoot, "steamapps") };

        string vdf = IOPath.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!IOFile.Exists(vdf)) return paths;

        foreach (string line in IOFile.ReadAllLines(vdf))
        {
            var m = Regex.Match(line, @"""path""\s+""(.+?)""");
            if (m.Success)
                paths.Add(IOPath.Combine(m.Groups[1].Value, "steamapps"));
        }

        return paths.Distinct().ToList();
    }

    static bool TryParseManifest(string path, out string? appId, out string? name)
    {
        appId = null; name = null;
        try
        {
            foreach (string line in IOFile.ReadAllLines(path))
            {
                var m = Regex.Match(line, @"""(\w+)""\s+""(.+?)""");
                if (!m.Success) continue;
                if (m.Groups[1].Value == "appid") appId = m.Groups[2].Value;
                if (m.Groups[1].Value == "name")  name  = m.Groups[2].Value;
                if (appId != null && name != null) return true;
            }
        }
        catch { }
        return false;
    }

    static bool IsGame(string name)
    {
        string l = name.ToLower();
        return !l.Contains("proton")           &&
               !l.Contains("runtime")          &&
               !l.Contains("redistributable")  &&
               !l.Contains("directx")          &&
               !l.Contains("vcredist")         &&
               !l.StartsWith("steam");
    }

    static string? FindSteamArt(string appId)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] cacheRoots =
        [
            IOPath.Combine(home, ".local", "share", "Steam", "appcache", "librarycache"),
            IOPath.Combine(home, ".steam", "steam",          "appcache", "librarycache"),
            IOPath.Combine(home, ".steam", "root",           "appcache", "librarycache"),
        ];

        foreach (var cache in cacheRoots.Where(IODirectory.Exists))
        {
            string[] candidates =
            [
                IOPath.Combine(cache, $"{appId}_library_hero.jpg"),
                IOPath.Combine(cache, $"{appId}_library_600x900.jpg"),
                IOPath.Combine(cache, $"{appId}_header.jpg"),
            ];
            var found = candidates.FirstOrDefault(IOFile.Exists);
            if (found != null) return found;
        }
        return null;
    }

    // Golden-angle hue spread so nearby app IDs get distinct colours
    static Color AppIdToColor(int appId)
    {
        double hue = appId * 137.508 % 360;
        return HslToRgb(hue, 0.55, 0.28);
    }

    static Color HslToRgb(double h, double s, double l)
    {
        double c = (1 - Math.Abs(2 * l - 1)) * s;
        double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
        double m = l - c / 2;
        double r, g, b;

        if      (h < 60)  { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else              { r = c; g = 0; b = x; }

        return Color.FromRgb(
            (byte)((r + m) * 255),
            (byte)((g + m) * 255),
            (byte)((b + m) * 255));
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        TilesCanvas.SizeChanged += (_, _) => DrawTiles();

        StoreButton.PointerPressed    += (_, _) => ActivateTopBarItem(0);
        SettingsButton.PointerPressed += (_, _) => ActivateTopBarItem(1);
        MenuButton.PointerPressed     += (_, _) => ActivateTopBarItem(2);

        _clockTimer = new Timer(
            _ => Dispatcher.UIThread.Post(UpdateClock),
            null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        UpdateClock();
        UpdateInfo();
        UpdateBackground();
    }

    void UpdateClock() =>
        ClockText.Text = DateTime.Now.ToString("HH:mm");

    Bitmap? GetWallpaper(int index)
    {
        if (_wallpapers.TryGetValue(index, out var cached)) return cached;

        var item = Items[index];

        // Steam appcache art takes priority
        if (item.WallpaperPath != null && IOFile.Exists(item.WallpaperPath))
        {
            var bmp = new Bitmap(item.WallpaperPath);
            _wallpapers[index] = bmp;
            return bmp;
        }

        // Fall back to wallpapers/ folder matched by name
        foreach (var ext in ImageExtensions)
        {
            string path = IOPath.Combine(WallpaperDir, item.Name.ToLower() + ext);
            if (IOFile.Exists(path))
            {
                var bmp = new Bitmap(path);
                _wallpapers[index] = bmp;
                return bmp;
            }
        }

        _wallpapers[index] = null;
        return null;
    }

    void UpdateBackground()
    {
        var bmp = GetWallpaper(_current);
        WallpaperImage.Source = bmp;

        DimOverlay.Background = bmp != null
            ? new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint   = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(210, 0, 0, 0), 0.00),
                    new GradientStop(Color.FromArgb(80,  0, 0, 0), 0.35),
                    new GradientStop(Color.FromArgb(80,  0, 0, 0), 0.65),
                    new GradientStop(Color.FromArgb(220, 0, 0, 0), 1.00),
                }
            }
            : new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint   = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(65, Items[_current].AccentColor.R,
                                                        Items[_current].AccentColor.G,
                                                        Items[_current].AccentColor.B), 0),
                    new GradientStop(Colors.Black, 0.65),
                }
            };
    }

    void UpdateInfo()
    {
        ItemNameText.Text = Items[_current].Name.ToUpper();
        ItemDescText.Text = Items[_current].Description;
    }

    void DrawTiles()
    {
        TilesCanvas.Children.Clear();

        double cx = TilesCanvas.Bounds.Width  / 2;
        double cy = TilesCanvas.Bounds.Height * 0.9;
        if (cx <= 0) return;

        for (int pass = 0; pass < 2; pass++)
        for (int i   = 0; i   < Items.Length; i++)
        {
            int  dist = i - _current;
            bool sel  = dist == 0;
            if (pass == 0 && sel)  continue;
            if (pass == 1 && !sel) continue;

            int abs = Math.Abs(dist);
            if (abs >= TileSizes.Length) continue;

            double size = TileSizes[abs];
            double x    = cx + TileOffsets[abs] * Math.Sign(dist) - size / 2;
            double y    = cy - size / 2 - (sel ? 30 : 0);

            var tile = new Border
            {
                Width           = size,
                Height          = size,
                Background      = new SolidColorBrush(Items[i].AccentColor),
                CornerRadius    = new CornerRadius(10),
                Opacity         = TileOpacity[abs],
                BorderThickness = sel ? new Thickness(3) : new Thickness(0),
                BorderBrush     = new SolidColorBrush(Yellow),
                Child = new TextBlock
                {
                    Text                = Items[i].Icon,
                    FontSize            = sel ? 76 : size * 0.37,
                    FontWeight          = FontWeight.Bold,
                    Foreground          = sel ? new SolidColorBrush(Yellow) : Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center,
                }
            };

            Canvas.SetLeft(tile, x);
            Canvas.SetTop(tile, y);
            TilesCanvas.Children.Add(tile);
        }
    }

    void UpdateAll()
    {
        UpdateBackground();
        UpdateInfo();
        DrawTiles();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up when _layer == Layer.Tiles:
                _layer = Layer.TopBar;
                _topBarIndex = 0;
                UpdateTopBar();
                break;

            case Key.Down when _layer == Layer.TopBar:
                _layer = Layer.Tiles;
                UpdateTopBar();
                break;

            case Key.Left when _layer == Layer.TopBar && _topBarIndex > 0:
                _topBarIndex--;
                UpdateTopBar();
                break;

            case Key.Right when _layer == Layer.TopBar && _topBarIndex < 2:
                _topBarIndex++;
                UpdateTopBar();
                break;

            case Key.Left when _layer == Layer.Tiles && _current > 0:
                _current--;
                UpdateAll();
                break;

            case Key.Right when _layer == Layer.Tiles && _current < Items.Length - 1:
                _current++;
                UpdateAll();
                break;

            case Key.Enter when _layer == Layer.TopBar:
                ActivateTopBarItem(_topBarIndex);
                break;

            case Key.Enter when _layer == Layer.Tiles:
                Launch(Items[_current]);
                break;

            case Key.Escape:
                Close();
                break;
        }
    }

    void UpdateTopBar()
    {
        Border[] buttons = [StoreButton, SettingsButton, MenuButton];
        for (int i = 0; i < buttons.Length; i++)
        {
            bool focused = _layer == Layer.TopBar && i == _topBarIndex;
            buttons[i].Background  = new SolidColorBrush(Color.FromArgb(focused ? (byte)80  : (byte)26,  255, 215, 0));
            buttons[i].BorderBrush = new SolidColorBrush(Color.FromArgb(focused ? (byte)220 : (byte)68,  255, 215, 0));
        }
    }

    void ActivateTopBarItem(int index)
    {
        switch (index)
        {
            case 0:
                Launch(new LaunchItem("Store", "", "", "xdg-open https://store.steampowered.com", default));
                break;
            case 1:
                // settings — coming soon
                break;
            case 2:
                // menu — coming soon
                break;
        }
    }

    static void Launch(LaunchItem item)
    {
        try
        {
            // setsid detaches the child into its own session so it survives after bash exits
            Process.Start(new ProcessStartInfo
            {
                FileName        = "/bin/bash",
                Arguments       = $"-c \"setsid {item.Command} >/dev/null 2>&1 &\"",
                UseShellExecute = false,
            });
        }
        catch { }
    }
}
