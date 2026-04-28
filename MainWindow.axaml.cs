using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
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
using System.Threading.Tasks;

namespace NovaBlackline;

record LaunchItem(string Name, string Icon, string Description, string Command, Color AccentColor, string? WallpaperPath = null);
record SettingRow(string Label, string[] Options, int Default, Action<int> Apply);
record ShopEntry(string Name, string Command, string Description);

enum Layer { Tiles, TopBar, Settings, Shop }

public partial class MainWindow : Window
{
    // ── Accent color (user-adjustable) ───────────────────────────────────────

    static readonly Color[] AccentColors =
    [
        Color.FromRgb(255, 215,   0),  // Yellow
        Color.FromRgb(255, 255, 255),  // White
        Color.FromRgb(  0, 210, 220),  // Cyan
        Color.FromRgb(220,  60,  60),  // Red
        Color.FromRgb( 80, 200,  80),  // Green
    ];

    Color _accent = Color.FromRgb(255, 215, 0);

    // ── Shop ─────────────────────────────────────────────────────────────────

    static readonly List<ShopEntry> ShopTabs = DetectShops();

    static readonly (string Name, string AppId, Color Color)[] FeaturedGames =
    [
        ("Counter-Strike 2",  "730",     Color.FromRgb(220, 140,  30)),
        ("Dota 2",            "570",     Color.FromRgb(180,  30,  30)),
        ("Team Fortress 2",   "440",     Color.FromRgb(200,  80,  20)),
        ("Warframe",          "230410",  Color.FromRgb( 30, 160, 200)),
        ("Path of Exile",     "238960",  Color.FromRgb( 80,  40, 140)),
        ("Destiny 2",         "1085660", Color.FromRgb( 50,  80, 170)),
        ("Apex Legends",      "1172470", Color.FromRgb(180,  60,   0)),
        ("Fall Guys",         "1097150", Color.FromRgb(180,  80, 200)),
    ];

    static List<ShopEntry> DetectShops()
    {
        var shops = new List<ShopEntry>
        {
            new("Nova Shop", "", "Free & featured games"),
        };

        // Steam
        if (FindSteamRoot() != null)
            shops.Add(new("Steam", "xdg-open https://store.steampowered.com", "Steam Store"));

        // Heroic Games Launcher — supports Epic Games and GOG
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? heroicCmd = FindBinary("heroic");
        if (heroicCmd == null && IOFile.Exists("/usr/bin/flatpak"))
        {
            string heroicData = IOPath.Combine(home, ".var", "app", "com.heroicgameslauncher.hgl");
            if (IODirectory.Exists(heroicData))
                heroicCmd = "flatpak run com.heroicgameslauncher.hgl";
        }
        if (heroicCmd != null)
        {
            shops.Add(new("Epic Games", heroicCmd, "Via Heroic Games Launcher"));
            shops.Add(new("GOG",        heroicCmd, "Via Heroic Games Launcher"));
        }
        else if (FindBinary("legendary") != null)
        {
            shops.Add(new("Epic Games", "xdg-open https://store.epicgames.com", "Epic Games Store"));
        }

        // Itch.io
        string? itchCmd = FindBinary("itch");
        if (itchCmd == null && IOFile.Exists("/usr/bin/flatpak"))
        {
            string itchData = IOPath.Combine(home, ".var", "app", "io.itch.itch");
            if (IODirectory.Exists(itchData))
                itchCmd = "flatpak run io.itch.itch";
        }
        if (itchCmd != null)
            shops.Add(new("Itch.io", itchCmd, "Indie games marketplace"));

        return shops;
    }

    // ── Items ────────────────────────────────────────────────────────────────

    static readonly LaunchItem[] Items = BuildItems();

    static readonly string WallpaperDir =
        IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpapers");

    static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    static readonly double[] TileSizes   = [200, 155, 120, 90];
    static readonly double[] TileOffsets = [0,   265, 430, 560];
    static readonly double[] TileOpacity = [1.0, 0.6, 0.35, 0.18];

    // ── State ────────────────────────────────────────────────────────────────

    int   _current;
    Layer _layer       = Layer.Tiles;
    int   _topBarIndex = 0;
    int   _navRepeatMs = 280;

    // Settings state
    SettingRow[] _settings = [];
    int[]        _settingValues = [];
    int          _settingIndex  = 0;

    // Shop state
    const int ShopTilesPerRow = 4; // 900px panel - 60px margins = 840px / (190+12)px per tile
    int  _shopTabIndex    = 0;
    bool _shopInContent   = false;
    int  _shopGameIndex   = 0;

    Timer?                   _clockTimer;
    CancellationTokenSource? _animCts;
    bool                     _animating = true;
    readonly HashSet<string>    _monitoredControllers = new();
    readonly Dictionary<int, Bitmap?> _wallpapers     = new();

    // ── Item discovery ────────────────────────────────────────────────────────

    static LaunchItem[] BuildItems()
    {
        var items = new List<LaunchItem>();
        items.AddRange(DiscoverSteamGames());
        items.Add(new("Terminal", ">", "Command line",    ResolveTerminal(),                                 Color.FromRgb(30,  30,  30)));
        items.Add(new("Spotify",  "S", "Music streaming", ResolveApp("spotify",  "com.spotify.Client"),     Color.FromRgb(29,  185, 84)));
        items.Add(new("Discord",  "@", "Voice and chat",  ResolveApp("discord",  "com.discordapp.Discord"), Color.FromRgb(88,  101, 242)));
        return items.ToArray();
    }

    static string? FindBinary(string cmd)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] dirs = ["/usr/bin", "/usr/local/bin", "/bin", IOPath.Combine(home, ".local", "bin")];
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
                    Name:         name!,
                    Icon:         name![0].ToString().ToUpper(),
                    Description:  name!,
                    Command:      $"xdg-open steam://rungameid/{appId}",
                    AccentColor:  AppIdToColor(int.Parse(appId!)),
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
        return candidates.FirstOrDefault(p => IODirectory.Exists(IOPath.Combine(p, "steamapps")));
    }

    static List<string> GetSteamLibraryPaths(string steamRoot)
    {
        var paths = new List<string> { IOPath.Combine(steamRoot, "steamapps") };
        string vdf = IOPath.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!IOFile.Exists(vdf)) return paths;

        foreach (string line in IOFile.ReadAllLines(vdf))
        {
            var m = Regex.Match(line, @"""path""\s+""(.+?)""");
            if (m.Success) paths.Add(IOPath.Combine(m.Groups[1].Value, "steamapps"));
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
        return !l.Contains("proton") && !l.Contains("runtime") &&
               !l.Contains("redistributable") && !l.Contains("directx") &&
               !l.Contains("vcredist") && !l.StartsWith("steam");
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
        return Color.FromRgb((byte)((r+m)*255), (byte)((g+m)*255), (byte)((b+m)*255));
    }

    // ── UI ───────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        Cursor = new Cursor(StandardCursorType.None);
        InitSettings();

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
        StartControllerMonitor();
        PlayStartAnimation();
    }

    void UpdateClock() => ClockText.Text = DateTime.Now.ToString("HH:mm");

    // ── Start animation ───────────────────────────────────────────────────────
    // Uses Transitions (not Animation/KeyFrame) so properties stay at their
    // target value after each step — no revert flicker.

    async void PlayStartAnimation()
    {
        _animCts = new CancellationTokenSource();
        var ct   = _animCts.Token;

        StartLogoGroup.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(900),  Easing = new CubicEaseOut() }
        };
        StartTagline.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(700),  Easing = new CubicEaseOut() }
        };
        StartOverlay.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(1000), Easing = new CubicEaseIn()  }
        };

        try
        {
            StartLogoGroup.Opacity = 1.0;
            await Task.Delay(900 + 900, ct);   // fade-in + hold

            StartTagline.Opacity = 0.45;
            await Task.Delay(700 + 1600, ct);  // fade-in + hold

            StartOverlay.Opacity = 0.0;
            await Task.Delay(1000, ct);        // fade-out
        }
        catch (OperationCanceledException) { }

        FinishAnimation();
    }

    void SkipAnimation()
    {
        if (!_animating) return;
        _animCts?.Cancel();
    }

    void FinishAnimation()
    {
        StartLogoGroup.Transitions = null;
        StartTagline.Transitions   = null;
        StartOverlay.Transitions   = null;
        _animating = false;
        _animCts?.Dispose();
        _animCts = null;
        StartOverlay.IsVisible = false;
    }

    // ── Settings ─────────────────────────────────────────────────────────────

    void InitSettings()
    {
        _settings =
        [
            new("Background Blur",   ["Off", "Low", "Medium", "High"],          2, ApplyBlur),
            new("Accent Color",      ["Yellow", "White", "Cyan", "Red", "Green"], 0, ApplyAccent),
            new("Navigation Speed",  ["Slow", "Normal", "Fast"],                1, ApplyNavSpeed),
        ];
        _settingValues = _settings.Select(s => s.Default).ToArray();

        // Apply defaults without redrawing (UI not ready yet)
        ApplyBlur(_settingValues[0]);
        // accent and speed are already at default
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
        for (int i = 0; i < _settings.Length; i++)
        {
            bool sel     = i == _settingIndex;
            var  setting = _settings[i];
            int  valIdx  = _settingValues[i];

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
                Text                = setting.Label,
                FontSize            = 15,
                Foreground          = sel ? new SolidColorBrush(_accent) : Brushes.White,
                FontWeight          = sel ? FontWeight.Bold : FontWeight.Normal,
                VerticalAlignment   = VerticalAlignment.Center,
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
                Text      = "◄",
                FontSize  = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(arrowAlpha, _accent.R, _accent.G, _accent.B)),
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
                Text      = "►",
                FontSize  = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(arrowAlpha, _accent.R, _accent.G, _accent.B)),
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

    void ApplyBlur(int idx)
    {
        double[] radii = [0, 8, 18, 30];
        WallpaperImage.Effect = new BlurEffect { Radius = radii[idx] };
    }

    void ApplyAccent(int idx)
    {
        _accent = AccentColors[idx];
        DrawTiles();
        UpdateTopBar();
        DrawSettingsRows();
    }

    void ApplyNavSpeed(int idx)
    {
        int[] delays = [420, 280, 150];
        _navRepeatMs = delays[idx];
    }

    // ── Wallpaper / background ────────────────────────────────────────────────

    Bitmap? GetWallpaper(int index)
    {
        if (_wallpapers.TryGetValue(index, out var cached)) return cached;
        var item = Items[index];

        if (item.WallpaperPath != null && IOFile.Exists(item.WallpaperPath))
        {
            var bmp = new Bitmap(item.WallpaperPath);
            _wallpapers[index] = bmp;
            return bmp;
        }
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
                StartPoint    = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0.5, 1, RelativeUnit.Relative),
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
                StartPoint    = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0.5, 1, RelativeUnit.Relative),
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
        double cy = TilesCanvas.Bounds.Height * 0.62;
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
                BorderBrush     = new SolidColorBrush(_accent),
                Child = new TextBlock
                {
                    Text                = Items[i].Icon,
                    FontSize            = sel ? 76 : size * 0.37,
                    FontWeight          = FontWeight.Bold,
                    Foreground          = sel ? new SolidColorBrush(_accent) : Brushes.White,
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

    // ── Input ────────────────────────────────────────────────────────────────

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_animating) { SkipAnimation(); return; }

        if (_layer == Layer.Shop)
        {
            if (_shopInContent)
            {
                switch (e.Key)
                {
                    case Key.Left when _shopGameIndex > 0:
                        _shopGameIndex--; DrawShopContent(); break;
                    case Key.Right when _shopGameIndex < FeaturedGames.Length - 1:
                        _shopGameIndex++; DrawShopContent(); break;
                    case Key.Up:
                        if (_shopGameIndex < ShopTilesPerRow)
                        { _shopInContent = false; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); }
                        else
                        { _shopGameIndex -= ShopTilesPerRow; DrawShopContent(); }
                        break;
                    case Key.Down:
                    {
                        int next = _shopGameIndex + ShopTilesPerRow;
                        if (next < FeaturedGames.Length) { _shopGameIndex = next; }
                        else if (_shopGameIndex / ShopTilesPerRow < (FeaturedGames.Length - 1) / ShopTilesPerRow)
                        { _shopGameIndex = FeaturedGames.Length - 1; }
                        DrawShopContent(); break;
                    }
                    case Key.Enter:
                        var (_, appId, _) = FeaturedGames[_shopGameIndex];
                        Launch(new LaunchItem("", "", "", $"xdg-open steam://store/{appId}", default)); break;
                    case Key.Escape:
                        _shopInContent = false; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); break;
                }
            }
            else
            {
                switch (e.Key)
                {
                    case Key.Left when _shopTabIndex > 0:
                        _shopTabIndex--; _shopGameIndex = 0; DrawShopTabs(); DrawShopContent(); break;
                    case Key.Right when _shopTabIndex < ShopTabs.Count - 1:
                        _shopTabIndex++; _shopGameIndex = 0; DrawShopTabs(); DrawShopContent(); break;
                    case Key.Down when ShopTabs[_shopTabIndex].Name == "Nova Shop":
                        _shopInContent = true; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); break;
                    case Key.Enter:
                        if (ShopTabs[_shopTabIndex].Name == "Nova Shop")
                        { _shopInContent = true; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); }
                        else ActivateCurrentShopTab();
                        break;
                    case Key.Escape: CloseShop(); break;
                }
            }
            return;
        }

        if (_layer == Layer.Settings)
        {
            switch (e.Key)
            {
                case Key.Up   when _settingIndex > 0:                       _settingIndex--; DrawSettingsRows(); break;
                case Key.Down when _settingIndex < _settings.Length - 1:    _settingIndex++; DrawSettingsRows(); break;
                case Key.Left:  ChangeSettingValue(-1); break;
                case Key.Right: ChangeSettingValue(+1); break;
                case Key.Escape: CloseSettings(); break;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Up when _layer == Layer.Tiles:
                _layer = Layer.TopBar; _topBarIndex = 0; UpdateTopBar(); break;

            case Key.Down when _layer == Layer.TopBar:
                _layer = Layer.Tiles; UpdateTopBar(); break;

            case Key.Left when _layer == Layer.TopBar && _topBarIndex > 0:
                _topBarIndex--; UpdateTopBar(); break;

            case Key.Right when _layer == Layer.TopBar && _topBarIndex < 2:
                _topBarIndex++; UpdateTopBar(); break;

            case Key.Left when _layer == Layer.Tiles && _current > 0:
                _current--; UpdateAll(); break;

            case Key.Right when _layer == Layer.Tiles && _current < Items.Length - 1:
                _current++; UpdateAll(); break;

            case Key.Enter when _layer == Layer.TopBar:
                ActivateTopBarItem(_topBarIndex); break;

            case Key.Enter when _layer == Layer.Tiles:
                Launch(Items[_current]); break;

            case Key.Escape:
                Close(); break;
        }
    }

    void UpdateTopBar()
    {
        Border[] buttons = [StoreButton, SettingsButton, MenuButton];
        for (int i = 0; i < buttons.Length; i++)
        {
            bool focused = _layer == Layer.TopBar && i == _topBarIndex;
            buttons[i].Background  = new SolidColorBrush(Color.FromArgb(focused ? (byte)80  : (byte)26,  _accent.R, _accent.G, _accent.B));
            buttons[i].BorderBrush = new SolidColorBrush(Color.FromArgb(focused ? (byte)220 : (byte)68,  _accent.R, _accent.G, _accent.B));
        }
    }

    void ActivateTopBarItem(int index)
    {
        switch (index)
        {
            case 0: OpenShop();     break;
            case 1: OpenSettings(); break;
            case 2: /* menu — coming soon */ break;
        }
    }

    // ── Shop ─────────────────────────────────────────────────────────────────

    void OpenShop()
    {
        _layer          = Layer.Shop;
        _shopTabIndex   = 0;
        _shopInContent  = false;
        _shopGameIndex  = 0;
        ShopOverlay.IsVisible = true;
        DrawShopTabs();
        DrawShopContent();
        UpdateShopFooter();
    }

    void UpdateShopFooter()
    {
        ShopFooterHint.Text = _shopInContent
            ? "← → ↑ ↓  navigate        ENTER  launch        ↑ (first row)  back to tabs        ESC  close"
            : "← →  switch tab        ↓  browse games        ENTER  open        ESC  close";
    }

    void CloseShop()
    {
        _layer = Layer.Tiles;
        ShopOverlay.IsVisible = false;
    }

    void DrawShopTabs()
    {
        ShopTabBar.Children.Clear();
        for (int i = 0; i < ShopTabs.Count; i++)
        {
            bool sel = i == _shopTabIndex;
            int  idx = i;
            var tab = new Border
            {
                Padding         = new Thickness(22, 0),
                Background      = sel
                    ? new SolidColorBrush(Color.FromArgb(40, _accent.R, _accent.G, _accent.B))
                    : Brushes.Transparent,
                BorderThickness = new Thickness(0, 0, 0, sel ? 2 : 0),
                BorderBrush     = new SolidColorBrush(sel ? _accent : Colors.Transparent),
                Cursor          = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock
                {
                    Text              = ShopTabs[i].Name,
                    FontSize          = 14,
                    FontWeight        = sel ? FontWeight.Bold : FontWeight.Normal,
                    Foreground        = sel
                        ? new SolidColorBrush(_accent)
                        : new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)),
                    VerticalAlignment = VerticalAlignment.Center,
                }
            };
            tab.PointerPressed += (_, _) => { _shopTabIndex = idx; DrawShopTabs(); DrawShopContent(); };
            ShopTabBar.Children.Add(tab);
        }
    }

    void DrawShopContent()
    {
        ShopContent.Children.Clear();
        var shop = ShopTabs[_shopTabIndex];

        if (shop.Name == "Nova Shop")
        {
            ShopContent.Children.Add(new TextBlock
            {
                Text       = "FREE & FEATURED",
                FontSize   = 12,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(_accent),
                Margin     = new Thickness(0, 0, 0, 16),
            });

            var wrap = new WrapPanel();
            for (int i = 0; i < FeaturedGames.Length; i++)
            {
                var (name, appId, color) = FeaturedGames[i];
                bool sel = _shopInContent && i == _shopGameIndex;
                string aid = appId;
                int    idx = i;

                var tile = new Border
                {
                    Width           = 190,
                    Height          = 110,
                    Background      = new SolidColorBrush(color),
                    CornerRadius    = new CornerRadius(8),
                    Margin          = new Thickness(0, 0, 12, 12),
                    Cursor          = new Cursor(StandardCursorType.Hand),
                    BorderThickness = new Thickness(sel ? 3 : 0),
                    BorderBrush     = new SolidColorBrush(_accent),
                    Opacity         = !_shopInContent || sel ? 1.0 : 0.55,
                    Child = new TextBlock
                    {
                        Text              = name,
                        FontSize          = 13,
                        FontWeight        = FontWeight.Bold,
                        Foreground        = Brushes.White,
                        TextWrapping      = TextWrapping.Wrap,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin            = new Thickness(12, 0, 12, 12),
                    }
                };
                tile.PointerPressed += (_, _) =>
                {
                    _shopInContent = true;
                    _shopGameIndex = idx;
                    Launch(new LaunchItem("", "", "", $"xdg-open steam://store/{aid}", default));
                };
                wrap.Children.Add(tile);
            }
            ShopContent.Children.Add(wrap);
        }
        else
        {
            ShopContent.Children.Add(new TextBlock
            {
                Text       = shop.Name.ToUpper(),
                FontSize   = 30,
                FontWeight = FontWeight.Bold,
                Foreground = new SolidColorBrush(_accent),
                Margin     = new Thickness(0, 16, 0, 8),
            });
            ShopContent.Children.Add(new TextBlock
            {
                Text       = shop.Description,
                FontSize   = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)),
                Margin     = new Thickness(0, 0, 0, 32),
            });
            ShopContent.Children.Add(new TextBlock
            {
                Text       = "Press ENTER to open  →",
                FontSize   = 15,
                Foreground = new SolidColorBrush(_accent),
            });
        }
    }

    void ActivateCurrentShopTab()
    {
        var shop = ShopTabs[_shopTabIndex];
        if (!string.IsNullOrEmpty(shop.Command))
            Launch(new LaunchItem(shop.Name, "", "", shop.Command, default));
    }

    // ── Controller (evdev) ───────────────────────────────────────────────────

    const int    EvStructSize  = 24;
    const int    EvTypeOffset  = 16;
    const int    EvCodeOffset  = 18;
    const int    EvValueOffset = 20;
    const ushort EV_KEY        = 1, EV_ABS = 3;
    const ushort BTN_SOUTH     = 0x130;
    const ushort BTN_EAST      = 0x131;
    const ushort BTN_START     = 0x13B;
    const ushort BTN_SELECT    = 0x13A;
    const ushort ABS_X         = 0;
    const ushort ABS_Y         = 1;
    const ushort ABS_HAT0X     = 16;
    const ushort ABS_HAT0Y     = 17;

    void StartControllerMonitor() =>
        new Thread(ControllerMonitorLoop) { IsBackground = true, Name = "ControllerMonitor" }.Start();

    void ControllerMonitorLoop()
    {
        while (true)
        {
            if (IODirectory.Exists("/dev/input"))
            {
                foreach (string dev in IODirectory.GetFiles("/dev/input", "event*"))
                {
                    if (IsGamepadDevice(dev) && _monitoredControllers.Add(dev))
                    {
                        string d = dev;
                        new Thread(() => ReadEvdevController(d)) { IsBackground = true }.Start();
                    }
                }
            }
            Thread.Sleep(2000);
        }
    }

    static bool IsGamepadDevice(string eventDev)
    {
        try
        {
            string name = IOFile.ReadAllText(
                $"/sys/class/input/{IOPath.GetFileName(eventDev)}/device/name").ToLower().Trim();
            return name.Contains("controller") || name.Contains("gamepad")   ||
                   name.Contains("joystick")   || name.Contains("nintendo")  ||
                   name.Contains("xbox")       || name.Contains("dualshock") ||
                   name.Contains("dualsense")  || name.Contains("pro con");
        }
        catch { return false; }
    }

    void ReadEvdevController(string device)
    {
        try
        {
            using var stream = new System.IO.FileStream(device,
                System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.ReadWrite);

            var buf           = new byte[EvStructSize];
            var axisLastFired = new Dictionary<ushort, DateTime>();
            var axisActive    = new Dictionary<ushort, bool>();

            while (stream.Read(buf, 0, EvStructSize) == EvStructSize)
            {
                ushort type  = BitConverter.ToUInt16(buf, EvTypeOffset);
                ushort code  = BitConverter.ToUInt16(buf, EvCodeOffset);
                int    value = BitConverter.ToInt32 (buf, EvValueOffset);

                if (type == EV_KEY && value == 1)
                {
                    ushort btn = code;
                    Dispatcher.UIThread.Post(() =>
                    {
                        switch (btn)
                        {
                            case BTN_SOUTH: case BTN_START:  OnControllerEnter(); break;
                            case BTN_EAST:  case BTN_SELECT: OnControllerBack();  break;
                        }
                    });
                }
                else if (type == EV_ABS)
                {
                    bool isHat  = code == ABS_HAT0X || code == ABS_HAT0Y;
                    bool active = isHat ? value != 0 : Math.Abs(value) > 16000;

                    if (!active) { axisActive[code] = false; axisLastFired.Remove(code); continue; }

                    var  now     = DateTime.UtcNow;
                    bool first   = !axisActive.GetValueOrDefault(code);
                    bool elapsed = !axisLastFired.TryGetValue(code, out var last)
                                   || (now - last).TotalMilliseconds > _navRepeatMs;

                    if (!first && !elapsed) continue;

                    axisActive[code]    = true;
                    axisLastFired[code] = now;

                    int dir = value > 0 ? 1 : -1; ushort axis = code;
                    Dispatcher.UIThread.Post(() =>
                    {
                        switch (axis)
                        {
                            case ABS_X: case ABS_HAT0X: if (dir < 0) OnControllerLeft();  else OnControllerRight(); break;
                            case ABS_Y: case ABS_HAT0Y: if (dir < 0) OnControllerUp();    else OnControllerDown();  break;
                        }
                    });
                }
            }
        }
        catch { _monitoredControllers.Remove(device); }
    }

    void OnControllerLeft()
    {
        if (_layer == Layer.Shop)
        {
            if (_shopInContent && _shopGameIndex > 0)           { _shopGameIndex--; DrawShopContent(); }
            else if (!_shopInContent && _shopTabIndex > 0)      { _shopTabIndex--; _shopGameIndex = 0; DrawShopTabs(); DrawShopContent(); }
            return;
        }
        if (_layer == Layer.Settings)                           { ChangeSettingValue(-1); return; }
        if (_layer == Layer.TopBar && _topBarIndex > 0)         { _topBarIndex--; UpdateTopBar(); return; }
        if (_layer == Layer.Tiles  && _current > 0)             { _current--; UpdateAll(); }
    }

    void OnControllerRight()
    {
        if (_layer == Layer.Shop)
        {
            if (_shopInContent && _shopGameIndex < FeaturedGames.Length - 1) { _shopGameIndex++; DrawShopContent(); }
            else if (!_shopInContent && _shopTabIndex < ShopTabs.Count - 1)  { _shopTabIndex++; _shopGameIndex = 0; DrawShopTabs(); DrawShopContent(); }
            return;
        }
        if (_layer == Layer.Settings)                                { ChangeSettingValue(+1); return; }
        if (_layer == Layer.TopBar && _topBarIndex < 2)              { _topBarIndex++; UpdateTopBar(); return; }
        if (_layer == Layer.Tiles  && _current < Items.Length - 1)   { _current++; UpdateAll(); }
    }

    void OnControllerUp()
    {
        if (_layer == Layer.Shop)
        {
            if (_shopInContent)
            {
                if (_shopGameIndex < ShopTilesPerRow)
                { _shopInContent = false; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); }
                else
                { _shopGameIndex -= ShopTilesPerRow; DrawShopContent(); }
            }
            return;
        }
        if (_layer == Layer.Settings && _settingIndex > 0)           { _settingIndex--; DrawSettingsRows(); return; }
        if (_layer == Layer.Tiles)                                    { _layer = Layer.TopBar; _topBarIndex = 0; UpdateTopBar(); }
    }

    void OnControllerDown()
    {
        if (_layer == Layer.Shop)
        {
            if (_shopInContent)
            {
                int next = _shopGameIndex + ShopTilesPerRow;
                if (next < FeaturedGames.Length) { _shopGameIndex = next; }
                else if (_shopGameIndex / ShopTilesPerRow < (FeaturedGames.Length - 1) / ShopTilesPerRow)
                { _shopGameIndex = FeaturedGames.Length - 1; }
                DrawShopContent();
            }
            else if (ShopTabs[_shopTabIndex].Name == "Nova Shop")
            { _shopInContent = true; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); }
            return;
        }
        if (_layer == Layer.Settings && _settingIndex < _settings.Length - 1) { _settingIndex++; DrawSettingsRows(); return; }
        if (_layer == Layer.TopBar)                                            { _layer = Layer.Tiles; UpdateTopBar(); }
    }

    void OnControllerEnter()
    {
        if (_animating) { SkipAnimation(); return; }
        if (_layer == Layer.Shop)
        {
            if (_shopInContent) { var (_, appId, _) = FeaturedGames[_shopGameIndex]; Launch(new LaunchItem("", "", "", $"xdg-open steam://store/{appId}", default)); }
            else if (ShopTabs[_shopTabIndex].Name == "Nova Shop") { _shopInContent = true; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); }
            else ActivateCurrentShopTab();
            return;
        }
        if (_layer == Layer.Settings) return;
        if (_layer == Layer.TopBar)   ActivateTopBarItem(_topBarIndex);
        else                          Launch(Items[_current]);
    }

    void OnControllerBack()
    {
        if (_animating) { SkipAnimation(); return; }
        if (_layer == Layer.Shop)     { if (_shopInContent) { _shopInContent = false; DrawShopTabs(); DrawShopContent(); UpdateShopFooter(); } else CloseShop(); return; }
        if (_layer == Layer.Settings) { CloseSettings(); return; }
        if (_layer == Layer.TopBar)   { _layer = Layer.Tiles; UpdateTopBar(); return; }
        Close();
    }

    static void Launch(LaunchItem item)
    {
        try
        {
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
