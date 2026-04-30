using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NovaBlackline;

public partial class MainWindow
{
    static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static readonly string ApiKeyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "nova-blackline", "steam_api_key");

    static readonly string CoverCacheDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".cache", "nova-blackline", "covers");

    static readonly string[] LibraryCacheRoots =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Steam", "appcache", "librarycache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "steam", "appcache", "librarycache"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".steam", "root",  "appcache", "librarycache"),
    ];

    // Bitmap cache persists for the app lifetime — survives open/close of the panel.
    static readonly Dictionary<string, Bitmap> _bitmapCache = new();

    static List<LibraryEntry>? _cachedLibraryGames;

    List<LibraryEntry>               _libraryGames   = [];
    readonly Dictionary<string, Image>  _coverImages  = new();
    readonly Dictionary<string, Border> _placeholders = new();
    int    _librarySelectedIndex = 0;
    string _libraryStatus        = "";

    const int TileW              = 190;
    const int TileH              = 285; // 2:3 ratio
    const int TileGap            = 8;
    const int VirtualizeAt       = 150; // switch to scroll-based loading above this
    const int ScrollBuffer       = 20;  // extra tiles preloaded outside the viewport

    // ── Open / Close ─────────────────────────────────────────────────────────

    void OpenLibrary()
    {
        _layer = Layer.Library;
        LibraryOverlay.IsVisible = true;
        _librarySelectedIndex = 0;

        LibraryScrollViewer.ScrollChanged -= OnLibraryScroll;

        if (_cachedLibraryGames != null)
        {
            _libraryGames = _cachedLibraryGames;
            LibrarySubtitle.Text = BuildSubtitle();
            DrawLibraryContent();
            BeginCoverLoading();
        }
        else
        {
            _libraryGames = [];
            _libraryStatus = "Loading your Steam library…";
            LibrarySubtitle.Text = "";
            DrawLibraryContent();
            _ = LoadLibraryAsync();
        }
    }

    void CloseLibrary()
    {
        LibraryScrollViewer.ScrollChanged -= OnLibraryScroll;
        _layer = Layer.Tiles;
        LibraryOverlay.IsVisible = false;
    }

    void BeginCoverLoading()
    {
        if (_libraryGames.Count > VirtualizeAt)
        {
            // Large library: preload first VirtualizeAt then load on scroll.
            LibraryScrollViewer.ScrollChanged += OnLibraryScroll;
            _ = LoadCoverRange(0, VirtualizeAt - 1);
        }
        else
        {
            _ = LoadCoverRange(0, _libraryGames.Count - 1);
        }
    }

    void OnLibraryScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (_layer != Layer.Library) return;
        _ = LoadVisibleCovers();
    }

    async Task LoadVisibleCovers()
    {
        int    cols   = LibraryCols();
        double rowH   = TileH + TileGap * 2 + 50;
        double scrollY = LibraryScrollViewer.Offset.Y;
        double viewH  = LibraryScrollViewer.Bounds.Height;

        int firstRow = Math.Max(0, (int)(scrollY / rowH) - 1);
        int lastRow  = (int)((scrollY + viewH) / rowH) + 2;

        int start = Math.Max(0, firstRow * cols - ScrollBuffer);
        int end   = Math.Min(_libraryGames.Count - 1, (lastRow + 1) * cols + ScrollBuffer);

        await LoadCoverRange(start, end);
    }

    // ── Library data fetch ───────────────────────────────────────────────────

    async Task LoadLibraryAsync()
    {
        string? apiKey = ReadApiKey();
        if (apiKey == null) { SetLibraryStatus(SetupMessage()); return; }

        try
        {
            string? steamId = GetMostRecentSteamId64();
            if (steamId == null)
            {
                SetLibraryStatus("Could not detect your Steam account.\nMake sure Steam has been launched at least once.");
                return;
            }

            var (games, error) = await FetchOwnedGamesAsync(apiKey, steamId);
            if (error != null) { SetLibraryStatus(error); return; }

            var installedIds = new HashSet<string>(
                DetectedGames.Select(g => g.SteamAppId).OfType<string>());

            var all = games
                .Select(g => new LibraryEntry(g.AppId, g.Name, installedIds.Contains(g.AppId)))
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _cachedLibraryGames = all;
            _libraryGames       = all;
            _libraryStatus      = "";

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LibrarySubtitle.Text = BuildSubtitle();
                DrawLibraryContent();
                BeginCoverLoading();
            });
        }
        catch (HttpRequestException) { SetLibraryStatus("Could not reach Steam. Check your internet connection."); }
        catch (Exception ex)         { SetLibraryStatus($"Error: {ex.Message}"); }
    }

    string BuildSubtitle()
    {
        int installed = _libraryGames.Count(g => g.Installed);
        return $"{_libraryGames.Count} games  ·  {installed} installed";
    }

    static string? ReadApiKey()
    {
        if (!File.Exists(ApiKeyPath)) return null;
        string key = File.ReadAllText(ApiKeyPath).Trim();
        return string.IsNullOrEmpty(key) ? null : key;
    }

    static string SetupMessage() =>
        "A free Steam API key is required to load your library.\n\n" +
        "1. Visit:  steamcommunity.com/dev/apikey\n" +
        "2. Log in and create a key (any domain name works)\n" +
        "3. Create this file and paste your key into it:\n\n" +
        $"   {ApiKeyPath}\n\n" +
        "Then reopen the Library.";

    static string? GetMostRecentSteamId64()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string path = Path.Combine(home, ".local", "share", "Steam", "config", "loginusers.vdf");
        if (!File.Exists(path)) return null;

        string? bestId = null, currentId = null;
        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            var m = Regex.Match(line, @"^""(\d{15,})""$");
            if (m.Success) { currentId = m.Groups[1].Value; bestId ??= currentId; continue; }
            if (currentId != null && Regex.IsMatch(line, @"""MostRecent""\s+""1"""))
                bestId = currentId;
        }
        return bestId;
    }

    static async Task<(List<(string AppId, string Name)> Games, string? Error)> FetchOwnedGamesAsync(string apiKey, string steamId64)
    {
        string url = $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/" +
                     $"?key={apiKey}&steamid={steamId64}&include_appinfo=true&format=json";
        string json = await _http.GetStringAsync(url);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("response", out var resp))
            return ([], "Invalid response from Steam API. Check your API key.");
        if (!resp.TryGetProperty("games", out var gamesEl))
            return ([], "Steam returned no games. Make sure 'Game Details' is set to\nPublic in Steam → Profile → Edit Profile → Privacy Settings.");

        var list = new List<(string, string)>();
        foreach (var g in gamesEl.EnumerateArray())
        {
            if (!g.TryGetProperty("appid", out var idEl)) continue;
            string appId = idEl.GetInt32().ToString();
            string name  = g.TryGetProperty("name", out var n) ? n.GetString() ?? appId : appId;
            list.Add((appId, name));
        }
        return (list, null);
    }

    void SetLibraryStatus(string message)
    {
        _libraryStatus = message;
        Dispatcher.UIThread.Post(() => { LibrarySubtitle.Text = ""; DrawLibraryContent(); });
    }

    // ── Drawing ──────────────────────────────────────────────────────────────

    void DrawLibraryContent()
    {
        _coverImages.Clear();
        _placeholders.Clear();
        LibraryContent.Children.Clear();

        if (!string.IsNullOrEmpty(_libraryStatus))
        {
            LibraryContent.Children.Add(new TextBlock
            {
                Text         = _libraryStatus,
                FontSize     = 13,
                Foreground   = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin       = new Thickness(4, 40, 4, 0),
            });
            return;
        }

        if (_libraryGames.Count == 0) return;

        var wrap = new WrapPanel { Orientation = Orientation.Horizontal };

        for (int i = 0; i < _libraryGames.Count; i++)
        {
            var game = _libraryGames[i];
            bool sel = i == _librarySelectedIndex;
            string capturedId  = game.AppId;
            int    capturedIdx = i;

            // Restore already-loaded bitmap immediately — prevents flicker on redraw.
            var coverImg = new Image
            {
                Width               = TileW,
                Height              = TileH,
                Stretch             = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Source              = _bitmapCache.GetValueOrDefault(game.AppId),
            };
            _coverImages[game.AppId] = coverImg;

            bool alreadyCached = _bitmapCache.ContainsKey(game.AppId);
            var placeholder = new Border
            {
                Width      = TileW,
                Height     = TileH,
                Background = new SolidColorBrush(AppIdToColor(int.TryParse(game.AppId, out int aid) ? aid : 0)),
                IsVisible  = !alreadyCached,
            };
            _placeholders[game.AppId] = placeholder;

            var coverGrid = new Grid { Width = TileW, Height = TileH };
            coverGrid.Children.Add(placeholder);
            coverGrid.Children.Add(coverImg);

            var badge = new Border
            {
                Background          = new SolidColorBrush(Color.FromArgb(game.Installed ? (byte)200 : (byte)160, 0, 0, 0)),
                CornerRadius        = new CornerRadius(4),
                Padding             = new Thickness(8, 3),
                Margin              = new Thickness(0, 5, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child               = new TextBlock
                {
                    Text       = game.Installed ? "INSTALLED" : "NOT INSTALLED",
                    FontSize   = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(game.Installed
                        ? _accent
                        : Color.FromArgb(100, 255, 255, 255)),
                },
            };

            var nameLabel = new TextBlock
            {
                Text                = game.Name,
                FontSize            = 11,
                Foreground          = new SolidColorBrush(sel ? _accent : Color.FromArgb(190, 255, 255, 255)),
                FontWeight          = sel ? FontWeight.Bold : FontWeight.Normal,
                TextTrimming        = Avalonia.Media.TextTrimming.CharacterEllipsis,
                TextWrapping        = Avalonia.Media.TextWrapping.NoWrap,
                MaxWidth            = TileW,
                Margin              = new Thickness(0, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment       = Avalonia.Media.TextAlignment.Center,
            };

            var inner = new StackPanel();
            inner.Children.Add(coverGrid);
            inner.Children.Add(badge);
            inner.Children.Add(nameLabel);

            var tile = new Border
            {
                Margin          = new Thickness(TileGap),
                Opacity         = game.Installed ? 1.0 : 0.42,
                BorderThickness = new Thickness(2),
                BorderBrush     = new SolidColorBrush(sel ? _accent : Colors.Transparent),
                CornerRadius    = new CornerRadius(6),
                Cursor          = new Cursor(StandardCursorType.Hand),
                Child           = inner,
            };

            tile.PointerPressed += (_, _) =>
            {
                _librarySelectedIndex = capturedIdx;
                DrawLibraryContent();
                _ = LoadVisibleCovers();
            };
            tile.DoubleTapped += (_, _) => ActivateLibraryGame(capturedId, _libraryGames[capturedIdx].Installed);

            wrap.Children.Add(tile);
        }

        LibraryContent.Children.Add(wrap);
    }

    // ── Cover loading ────────────────────────────────────────────────────────

    async Task LoadCoverRange(int startIdx, int endIdx)
    {
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (_layer != Layer.Library) return;

            var game = _libraryGames[i];
            if (_bitmapCache.ContainsKey(game.AppId)) continue; // already in cache

            var bmp = await GetBitmapAsync(game.AppId);
            if (bmp == null) continue;

            _bitmapCache[game.AppId] = bmp;

            var b = bmp;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_coverImages.TryGetValue(game.AppId, out var img))
                    img.Source = b;
                if (_placeholders.TryGetValue(game.AppId, out var ph))
                    ph.IsVisible = false;
            });
        }
    }

    static async Task<Bitmap?> GetBitmapAsync(string appId)
    {
        // 1. Steam's local librarycache — {appId}_library_600x900.jpg
        string? localPath = FindPortraitCover(appId);
        if (localPath != null)
            try { return await Task.Run(() => new Bitmap(localPath)); } catch { }

        // 2. Our download cache
        string cachePath = Path.Combine(CoverCacheDir, $"{appId}.jpg");
        if (File.Exists(cachePath))
            try { return await Task.Run(() => new Bitmap(cachePath)); } catch { }

        // 3. Steam CDN
        return await DownloadCoverAsync(appId);
    }

    static string? FindPortraitCover(string appId)
    {
        foreach (string root in LibraryCacheRoots.Where(Directory.Exists))
        {
            string? path = FindSteamNamedJpg(root, appId, "library_600x900.jpg");
            if (path != null) return path;
        }
        return null;
    }

    static async Task<Bitmap?> DownloadCoverAsync(string appId)
    {
        string[] urls =
        [
            $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/library_600x900.jpg",
            $"https://cdn.akamai.steamstatic.com/steam/apps/{appId}/header.jpg",
        ];

        foreach (string url in urls)
        {
            try
            {
                await Task.Delay(30); // avoid hammering CDN
                byte[] bytes = await _http.GetByteArrayAsync(url);
                Directory.CreateDirectory(CoverCacheDir);
                await File.WriteAllBytesAsync(Path.Combine(CoverCacheDir, $"{appId}.jpg"), bytes);
                return new Bitmap(new MemoryStream(bytes));
            }
            catch { }
        }
        return null;
    }

    // ── Input ────────────────────────────────────────────────────────────────

    int LibraryCols() => Math.Max(1, (int)(LibraryContent.Bounds.Width / (TileW + TileGap * 2)));

    void HandleLibraryKey(Key key)
    {
        int cols  = LibraryCols();
        int count = _libraryGames.Count;

        switch (key)
        {
            case Key.Left  when _librarySelectedIndex > 0:
                _librarySelectedIndex--; RefreshAndScroll(); break;
            case Key.Right when _librarySelectedIndex < count - 1:
                _librarySelectedIndex++; RefreshAndScroll(); break;
            case Key.Up    when _librarySelectedIndex - cols >= 0:
                _librarySelectedIndex -= cols; RefreshAndScroll(); break;
            case Key.Down  when _librarySelectedIndex + cols < count:
                _librarySelectedIndex += cols; RefreshAndScroll(); break;
            case Key.Enter when count > 0:
                var g = _libraryGames[_librarySelectedIndex];
                ActivateLibraryGame(g.AppId, g.Installed); break;
            case Key.Escape:
                CloseLibrary(); break;
        }
    }

    void RefreshAndScroll()
    {
        DrawLibraryContent();
        _ = LoadVisibleCovers();
        ScrollLibraryToSelected();
    }

    void ScrollLibraryToSelected()
    {
        int    cols   = LibraryCols();
        int    row    = _librarySelectedIndex / cols;
        double rowH   = TileH + TileGap * 2 + 44;
        double vp     = LibraryScrollViewer.Bounds.Height;
        double target = row * rowH - vp / 2 + rowH / 2;
        LibraryScrollViewer.Offset = new Vector(0, Math.Max(0, target));
    }

    void ActivateLibraryGame(string appId, bool installed)
    {
        string cmd = installed
            ? $"xdg-open steam://rungameid/{appId}"
            : $"xdg-open steam://install/{appId}";
        Launch(new LaunchItem("", "", "", cmd, default));
    }

    void InstallSteamGame(string appId) => ActivateLibraryGame(appId, false);
}
