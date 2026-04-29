namespace NovaBlackline;

public partial class MainWindow
{
    void RebuildItems()
    {
        var list = new System.Collections.Generic.List<LaunchItem>(DetectedGames);

        if (_appsMode != 1) // not Off
        {
            foreach (var app in DetectedApps)
            {
                if (_appsMode == 0 || _customApps.Contains(app.Name))
                    list.Add(app);
            }
        }

        Items   = list.ToArray();
        _current = Items.Length > 0 ? System.Math.Min(_current, Items.Length - 1) : 0;
    }
}
