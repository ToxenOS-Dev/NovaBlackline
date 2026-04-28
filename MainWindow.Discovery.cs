using Avalonia.Media;
using IOPath      = System.IO.Path;
using IOFile      = System.IO.File;
using IODirectory = System.IO.Directory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NovaBlackline;

public partial class MainWindow
{
    static readonly Color[] AccentColors =
    [
        Color.FromRgb(255, 215,   0),
        Color.FromRgb(255, 255, 255),
        Color.FromRgb(  0, 210, 220),
        Color.FromRgb(220,  60,  60),
        Color.FromRgb( 80, 200,  80),
    ];

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

    static readonly LaunchItem[] Items = BuildItems();

    static readonly string WallpaperDir =
        IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "wallpapers");

    static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    static readonly string? DefaultWallpaperPath = FindDefaultWallpaper();

    static List<ShopEntry> DetectShops()
    {
        var shops = new List<ShopEntry>
        {
            new("Nova Shop", "", "Free & featured games"),
        };

        if (FindSteamRoot() != null)
            shops.Add(new("Steam", "xdg-open https://store.steampowered.com", "Steam Store"));

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

    static LaunchItem[] BuildItems()
    {
        var items = new List<LaunchItem>();
        items.AddRange(DiscoverSteamGames());
        items.Add(new("Terminal",    ">",  "Command line",          ResolveTerminal(),                                   Color.FromRgb( 30,  30,  30)));
        items.Add(new("Spotify",     "S",  "Music streaming",       ResolveApp("spotify",     "com.spotify.Client"),     Color.FromRgb( 29, 185,  84)));
        items.Add(new("Discord",     "@",  "Voice and chat",        ResolveApp("discord",     "com.discordapp.Discord"), Color.FromRgb( 88, 101, 242)));
        items.Add(new("Firefox",     "FF", "Web browser",           ResolveApp("firefox",     "org.mozilla.firefox"),    Color.FromRgb(255,  80,   0)));
        items.Add(new("Chrome",      "G",  "Web browser",           ResolveApp("google-chrome","com.google.Chrome"),     Color.FromRgb( 66, 133, 244)));
        items.Add(new("VSCode",      "</>","Code editor",           ResolveApp("code",        "com.visualstudio.code"),  Color.FromRgb( 0,  122, 204)));
        items.Add(new("Files",       "F",  "File manager",          ResolveApp("nautilus",    "org.gnome.Nautilus"),     Color.FromRgb( 53, 132, 228)));
        items.Add(new("Steam",       "ST", "Game platform",         ResolveApp("steam",       "com.valvesoftware.Steam"),Color.FromRgb( 23,  46,  73)));
        items.Add(new("OBS",         "●",  "Streaming & recording", ResolveApp("obs",         "com.obsproject.Studio"),  Color.FromRgb( 50,  50,  50)));
        items.Add(new("VLC",         "▶",  "Media player",          ResolveApp("vlc",         "org.videolan.VLC"),       Color.FromRgb(255, 165,   0)));
        items.Add(new("GIMP",        "G",  "Image editor",          ResolveApp("gimp",        "org.gimp.GIMP"),          Color.FromRgb( 93, 141,  52)));
        items.Add(new("Blender",     "B",  "3D creation suite",     ResolveApp("blender",     "org.blender.Blender"),    Color.FromRgb(234, 126,   0)));
        items.Add(new("Lutris",      "L",  "Game manager",          ResolveApp("lutris",      "net.lutris.Lutris"),      Color.FromRgb(255, 230, 100)));
        items.Add(new("Bottles",     "B",  "Run Windows apps",      ResolveApp("bottles",     "com.usebottles.bottles"), Color.FromRgb(240,  70,  70)));
        items.Add(new("Vesktop",     "V",  "Discord client",        ResolveApp("vesktop",     "dev.vencord.Vesktop"),    Color.FromRgb( 88, 101, 242)));
        items.Add(new("Telegram",    "✈",  "Messaging",             ResolveApp("telegram-desktop","org.telegram.desktop"),Color.FromRgb( 42, 174, 245)));
        items.Add(new("ProtonMail",  "P",  "Encrypted email",       "xdg-open https://mail.proton.me",                  Color.FromRgb(109,  74, 255)));
        items.Add(new("YouTube",     "▶",  "Video streaming",       "xdg-open https://youtube.com",                     Color.FromRgb(255,   0,   0)));
        items.Add(new("Twitch",      "T",  "Live streaming",        "xdg-open https://twitch.tv",                       Color.FromRgb(145,  70, 255)));
        items.Add(new("GitHub",      "GH", "Code hosting",          "xdg-open https://github.com",                      Color.FromRgb( 36,  41,  46)));
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
                if (!TryParseManifest(manifest, out var steamApp) || steamApp == null) continue;
                string appId = steamApp.AppId;
                string name  = steamApp.Name;
                if (!seen.Add(appId!)) continue;
                if (!IsGame(steamApp)) continue;

                yield return new LaunchItem(
                    Name:         name,
                    Icon:         name[0].ToString().ToUpper(),
                    Description:  name,
                    Command:      $"xdg-open steam://rungameid/{appId}",
                    AccentColor:  AppIdToColor(int.Parse(appId)),
                    WallpaperPath: FindSteamJpgArt(appId) ?? DefaultWallpaperPath,
                    SteamAppId:    appId,
                    SteamInstallDir: GetSteamInstallDir(libraryPath, steamApp.InstallDir));
            }
        }
    }

    static string? GetSteamInstallDir(string libraryPath, string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir)) return null;
        string path = IOPath.Combine(libraryPath, "common", installDir);
        return IODirectory.Exists(path) ? path : null;
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

    static bool TryParseManifest(string path, out SteamAppManifest? app)
    {
        app = default;
        string? appId = null;
        string? name = null;
        string? type = null;
        string? installDir = null;

        try
        {
            foreach (string line in IOFile.ReadAllLines(path))
            {
                var m = Regex.Match(line, @"""(\w+)""\s+""(.+?)""");
                if (!m.Success) continue;

                string key = m.Groups[1].Value.ToLowerInvariant();
                string value = m.Groups[2].Value;

                if (key == "appid") appId = value;
                if (key == "name")  name  = value;
                if (key == "type")  type  = value;
                if (key == "installdir") installDir = value;
            }
        }
        catch { }

        if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(name))
            return false;

        app = new SteamAppManifest(appId, name, type, installDir);
        return true;
    }

    static bool IsGame(SteamAppManifest app)
    {
        if (!string.IsNullOrWhiteSpace(app.Type))
            return app.Type.Equals("game", StringComparison.OrdinalIgnoreCase);

        string l = app.Name.ToLower();
        return !l.Contains("proton") && !l.Contains("runtime") &&
               !l.Contains("redistributable") && !l.Contains("directx") &&
               !l.Contains("vcredist") && !l.StartsWith("steam");
    }

    static string? FindSteamJpgArt(string appId)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] cacheRoots =
        [
            IOPath.Combine(home, ".local", "share", "Steam", "appcache", "librarycache"),
            IOPath.Combine(home, ".steam", "steam",          "appcache", "librarycache"),
            IOPath.Combine(home, ".steam", "root",           "appcache", "librarycache"),
        ];
        var existingCaches = cacheRoots.Where(IODirectory.Exists).ToArray();

        var hero = existingCaches
            .Select(cache => FindSteamNamedJpg(cache, appId, "library_hero.jpg"))
            .FirstOrDefault(path => path != null);
        if (hero != null) return hero;

        var header = existingCaches
            .Select(cache => FindSteamNamedJpg(cache, appId, "header.jpg"))
            .FirstOrDefault(path => path != null);
        if (header != null) return header;

        var hashedJpg = existingCaches
            .Select(cache => FindHashedSteamJpg(cache, appId))
            .FirstOrDefault(path => path != null);
        if (hashedJpg != null) return hashedJpg;

        return null;
    }

    static string? FindSteamNamedJpg(string cacheRoot, string appId, string fileName)
    {
        string appCache = IOPath.Combine(cacheRoot, appId);
        string flatCacheFile = IOPath.Combine(cacheRoot, $"{appId}_{fileName}");

        if (IOFile.Exists(IOPath.Combine(appCache, fileName)))
            return IOPath.Combine(appCache, fileName);

        if (IOFile.Exists(flatCacheFile))
            return flatCacheFile;

        if (!IODirectory.Exists(appCache)) return null;

        return IODirectory
            .EnumerateFiles(appCache, fileName, System.IO.SearchOption.AllDirectories)
            .OrderBy(path => path.Count(c => c == IOPath.DirectorySeparatorChar))
            .ThenBy(path => path)
            .FirstOrDefault();
    }

    static string? FindHashedSteamJpg(string cacheRoot, string appId)
    {
        string appCache = IOPath.Combine(cacheRoot, appId);
        if (!IODirectory.Exists(appCache)) return null;

        return IODirectory
            .EnumerateFiles(appCache, "*.jpg", System.IO.SearchOption.TopDirectoryOnly)
            .Where(path =>
            {
                string name = IOPath.GetFileName(path);
                return name != "library_hero_blur.jpg" &&
                       name != "library_600x900.jpg";
            })
            .OrderBy(path => IOPath.GetFileName(path))
            .FirstOrDefault();
    }

    static string? FindDefaultWallpaper()
    {
        string defaultWallpaper = IOPath.Combine(WallpaperDir, "Default-Wallpaper", "Default-Wallpaper.jpg");
        if (IOFile.Exists(defaultWallpaper)) return defaultWallpaper;

        foreach (var ext in ImageExtensions)
        {
            string path = IOPath.Combine(WallpaperDir, "default" + ext);
            if (IOFile.Exists(path)) return path;
        }

        string fallback = IOPath.Combine(WallpaperDir, "terminal.jpg");
        if (IOFile.Exists(fallback)) return fallback;

        return IODirectory.Exists(WallpaperDir)
            ? IODirectory.GetFiles(WallpaperDir)
                .FirstOrDefault(path => ImageExtensions.Contains(IOPath.GetExtension(path).ToLowerInvariant()))
            : null;
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
}
