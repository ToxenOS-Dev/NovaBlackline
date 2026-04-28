using Avalonia.Media;

namespace NovaBlackline;

public partial class MainWindow
{
    static readonly (string Label, System.Action<MainWindow> Activate)[] MenuItems =
    [
        ("Close", w => w.Close()),
    ];

    void OpenMenu()
    {
        _layer = Layer.Menu;
        _menuIndex = 0;
        MenuOverlay.IsVisible = true;
        DrawMenuItems();
    }

    void CloseMenu()
    {
        _layer = Layer.Tiles;
        MenuOverlay.IsVisible = false;
        UpdateTopBar();
    }

    void ActivateMenuItem() => MenuItems[_menuIndex].Activate(this);

    void DrawMenuItems()
    {
        bool sel = _menuIndex == 0;
        CloseAppButton.Background  = new SolidColorBrush(sel
            ? Color.FromArgb(0xFF,  110, 20, 20)
            : Color.FromArgb(0xAA,   26, 26, 26));
        CloseAppButton.BorderBrush = new SolidColorBrush(sel
            ? Color.FromArgb(0xFF, 200, 50, 50)
            : Color.FromArgb(0xBB,  51, 17, 17));
    }
}
