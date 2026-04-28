using System.Diagnostics;

namespace NovaBlackline;

public partial class MainWindow
{
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
