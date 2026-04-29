using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
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

    // cached across open/close so we don't refetch every time
    static List<(string AppId, string Name)>? _cachedLibraryGames;

    List<(string AppId, string Name)> _libraryGames = [];
    int _librarySelectedIndex = 0;
    string _libraryStatus = "";

    void OpenLibrary()
    {
        _layer = Layer.Library;
        LibraryOverlay.IsVisible = true;
        _librarySelectedIndex = 0;

        if (_cachedLibraryGames != null)
        {
            _libraryGames = _cachedLibraryGames;
            LibrarySubtitle.Text = $"{_libraryGames.Count} not installed";
            DrawLibraryContent();
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
        _layer = Layer.Tiles;
        LibraryOverlay.IsVisible = false;
    }

    async Task LoadLibraryAsync()
    {
        string? apiKey = ReadApiKey();
        if (apiKey == null)
        {
            SetLibraryStatus(SetupMessage());
            return;
        }

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

            var uninstalled = games
                .Where(g => !installedIds.Contains(g.AppId))
                .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _cachedLibraryGames = uninstalled;
            _libraryGames = uninstalled;
            _libraryStatus = "";

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                LibrarySubtitle.Text = $"{_libraryGames.Count} not installed";
                DrawLibraryContent();
            });
        }
        catch (HttpRequestException)
        {
            SetLibraryStatus("Could not reach Steam. Check your internet connection.");
        }
        catch (Exception ex)
        {
            SetLibraryStatus($"Error: {ex.Message}");
        }
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
        string loginUsersPath = Path.Combine(home, ".local", "share", "Steam", "config", "loginusers.vdf");
        if (!File.Exists(loginUsersPath)) return null;

        string? bestId = null;
        string? currentId = null;

        foreach (string rawLine in File.ReadAllLines(loginUsersPath))
        {
            string line = rawLine.Trim();
            var idMatch = Regex.Match(line, @"^""(\d{15,})""$");
            if (idMatch.Success)
            {
                currentId = idMatch.Groups[1].Value;
                bestId ??= currentId;
                continue;
            }
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

        if (!doc.RootElement.TryGetProperty("response", out var response))
            return ([], "Invalid response from Steam API. Check your API key.");

        if (!response.TryGetProperty("games", out var gamesEl))
            return ([], "Steam returned no games. Make sure 'Game Details' is set to\nPublic in Steam → Profile → Edit Profile → Privacy Settings.");

        var list = new List<(string AppId, string Name)>();
        foreach (var game in gamesEl.EnumerateArray())
        {
            if (!game.TryGetProperty("appid", out var idEl)) continue;
            string appId = idEl.GetInt32().ToString();
            string name  = game.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? appId : appId;
            list.Add((appId, name));
        }

        return (list, null);
    }

    void SetLibraryStatus(string message)
    {
        _libraryStatus = message;
        Dispatcher.UIThread.Post(() =>
        {
            LibrarySubtitle.Text = "";
            DrawLibraryContent();
        });
    }

    void DrawLibraryContent()
    {
        LibraryContent.Children.Clear();

        if (!string.IsNullOrEmpty(_libraryStatus))
        {
            LibraryContent.Children.Add(new TextBlock
            {
                Text = _libraryStatus,
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = Avalonia.Media.TextAlignment.Left,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(4, 40, 4, 0),
            });
            return;
        }

        if (_libraryGames.Count == 0) return;

        for (int i = 0; i < _libraryGames.Count; i++)
        {
            var (appId, name) = _libraryGames[i];
            bool sel = i == _librarySelectedIndex;
            string capturedId = appId;
            int capturedIdx = i;

            var row = new Border
            {
                Background = sel
                    ? new SolidColorBrush(Color.FromArgb(45, _accent.R, _accent.G, _accent.B))
                    : new SolidColorBrush(Color.FromArgb(10, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(sel
                    ? Color.FromArgb(160, _accent.R, _accent.G, _accent.B)
                    : Color.FromArgb(18, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 11),
                Margin = new Thickness(0, 0, 0, 6),
                Cursor = new Cursor(StandardCursorType.Hand),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            grid.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 14,
                FontWeight = sel ? FontWeight.Bold : FontWeight.Normal,
                Foreground = new SolidColorBrush(sel ? _accent : Color.FromArgb(200, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            });

            var installBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(50, _accent.R, _accent.G, _accent.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(130, _accent.R, _accent.G, _accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(18, 5),
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = "Install",
                    FontSize = 11,
                    FontWeight = FontWeight.Bold,
                    Foreground = new SolidColorBrush(_accent),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            installBtn.PointerPressed += (_, _) => InstallSteamGame(capturedId);
            Grid.SetColumn(installBtn, 1);
            grid.Children.Add(installBtn);

            row.Child = grid;
            row.PointerPressed += (_, _) => { _librarySelectedIndex = capturedIdx; DrawLibraryContent(); };

            LibraryContent.Children.Add(row);
        }
    }

    void HandleLibraryKey(Key key)
    {
        switch (key)
        {
            case Key.Up when _librarySelectedIndex > 0:
                _librarySelectedIndex--;
                DrawLibraryContent();
                ScrollLibraryToSelected();
                break;
            case Key.Down when _librarySelectedIndex < _libraryGames.Count - 1:
                _librarySelectedIndex++;
                DrawLibraryContent();
                ScrollLibraryToSelected();
                break;
            case Key.Enter when _libraryGames.Count > 0:
                InstallSteamGame(_libraryGames[_librarySelectedIndex].AppId);
                break;
            case Key.Escape:
                CloseLibrary();
                break;
        }
    }

    void ScrollLibraryToSelected()
    {
        const double rowHeight = 50;
        double viewport = LibraryScrollViewer.Bounds.Height;
        double target = _librarySelectedIndex * rowHeight - viewport / 2 + rowHeight / 2;
        LibraryScrollViewer.Offset = new Vector(0, Math.Max(0, target));
    }

    void InstallSteamGame(string appId) =>
        Launch(new LaunchItem("", "", "", $"xdg-open steam://install/{appId}", default));
}
