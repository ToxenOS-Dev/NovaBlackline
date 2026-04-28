using Avalonia.Media;
using System;

namespace NovaBlackline;

record LaunchItem(string Name, string Icon, string Description, string Command, Color AccentColor, string? WallpaperPath = null);
record SettingRow(string Label, string[] Options, int Default, Action<int> Apply);
record ShopEntry(string Name, string Command, string Description);
record SteamAppManifest(string AppId, string Name, string? Type);

enum Layer { Tiles, TopBar, Settings, Shop }
