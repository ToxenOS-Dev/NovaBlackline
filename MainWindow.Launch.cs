using Avalonia.Controls;
using Avalonia.Threading;
using IOFile = System.IO.File;
using IODirectory = System.IO.Directory;
using IOPath = System.IO.Path;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace NovaBlackline;

public partial class MainWindow
{
    async void Launch(LaunchItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.SteamAppId))
        {
            await LaunchSteamGame(item);
            return;
        }

        StartDetached(item.Command);
    }

    async Task LaunchSteamGame(LaunchItem item)
    {
        string appId = item.SteamAppId!;
        string command = BuildSteamLaunchCommand(appId);

        _gameSessionActive = true;
        Topmost = true;
        ApplyDisplayPlacement();
        WindowState = WindowState.FullScreen;
        LaunchCover.IsVisible = true;
        Show();
        Activate();
        Focus();

        try
        {
            StartDetached(command);
            bool gameStarted = await WaitForSteamGameStart(appId, item.SteamInstallDir);
            if (gameStarted)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Topmost = false;
                    Hide();
                });

                await WaitForSteamGameExit(appId, item.SteamInstallDir);
            }
        }
        finally
        {
            _gameSessionActive = false;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Topmost = false;
                LaunchCover.IsVisible = false;
                Show();
                ApplyDisplayPlacement();
                WindowState = WindowState.FullScreen;
                Activate();
                Focus();
            });
        }
    }

    string BuildSteamLaunchCommand(string appId)
    {
        string? steam = FindBinary("steam");
        if (steam == null)
            return $"xdg-open steam://rungameid/{appId}";

        return $"{steam} -silent -applaunch {appId}";
    }

    bool HasControllerConnected()
    {
        if (_monitoredControllers.Count > 0) return true;
        if (!IODirectory.Exists("/dev/input")) return false;

        try
        {
            return IODirectory
                .EnumerateFiles("/dev/input", "event*")
                .Any(IsGamepadDevice);
        }
        catch
        {
            return false;
        }
    }

    async Task<bool> WaitForSteamGameStart(string appId, string? installDir)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            if (IsSteamGameRunning(appId, installDir))
                return true;

            await Task.Delay(1000);
        }

        return false;
    }

    async Task WaitForSteamGameExit(string appId, string? installDir)
    {
        while (IsSteamGameRunning(appId, installDir))
            await Task.Delay(2000);
    }

    static bool IsSteamGameRunning(string appId, string? installDir)
    {
        foreach (string procDir in IODirectory.EnumerateDirectories("/proc"))
        {
            string pid = IOPath.GetFileName(procDir);
            if (!pid.All(char.IsDigit)) continue;

            string cmdline = ReadProcText(IOPath.Combine(procDir, "cmdline"));
            string environ = ReadProcText(IOPath.Combine(procDir, "environ"));
            if (string.IsNullOrEmpty(cmdline) && string.IsNullOrEmpty(environ)) continue;

            string text = (cmdline + "\n" + environ).Replace('\0', ' ');
            if (text.Contains($"/compatdata/{appId}/", StringComparison.Ordinal) ||
                text.Contains($"STEAM_COMPAT_APP_ID={appId}", StringComparison.Ordinal) ||
                text.Contains($"SteamAppId={appId}", StringComparison.Ordinal) ||
                text.Contains($"SteamGameId={appId}", StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(installDir) &&
                text.Contains(installDir, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static string ReadProcText(string path)
    {
        try { return IOFile.ReadAllText(path); }
        catch { return ""; }
    }

    void StartDetached(string command)
    {
        try
        {
            string environmentPrefix = BuildPrimaryDisplayEnvironmentPrefix();
            Process.Start(new ProcessStartInfo
            {
                FileName        = "/bin/bash",
                Arguments       = $"-c \"{environmentPrefix}setsid {command} >/dev/null 2>&1 &\"",
                UseShellExecute = false,
            });
        }
        catch { }
    }

    static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''")}'";
}
