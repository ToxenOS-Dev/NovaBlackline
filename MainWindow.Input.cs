using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NovaBlackline;

public partial class MainWindow
{
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_gameSessionActive) return;
        if (_animating) { SkipAnimation(); return; }

        if (_layer == Layer.Shop)
        {
            HandleShopKey(e.Key);
            return;
        }

        if (_layer == Layer.Menu)
        {
            switch (e.Key)
            {
                case Key.Up   when _menuIndex > 0:                      _menuIndex--; DrawMenuItems(); break;
                case Key.Down when _menuIndex < MenuItems.Length - 1:   _menuIndex++; DrawMenuItems(); break;
                case Key.Enter:  ActivateMenuItem(); break;
                case Key.Escape: CloseMenu(); break;
            }
            return;
        }

        if (_layer == Layer.Settings)
        {
            switch (e.Key)
            {
                case Key.Up   when _settingIndex > 0:                    _settingIndex--; DrawSettingsRows(); break;
                case Key.Down when _settingIndex < _settings.Length - 1: _settingIndex++; DrawSettingsRows(); break;
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
                SwitchToItem(_current - 1); break;

            case Key.Right when _layer == Layer.Tiles && _current < Items.Length - 1:
                SwitchToItem(_current + 1); break;

            case Key.Enter when _layer == Layer.TopBar:
                ActivateTopBarItem(_topBarIndex); break;

            case Key.Enter when _layer == Layer.Tiles:
                Launch(Items[_current]); break;

            case Key.Escape when _layer == Layer.TopBar:
                _layer = Layer.Tiles; UpdateTopBar(); break;
        }
    }

    void UpdateTopBar()
    {
        Border[] buttons = [StoreButton, SettingsButton, MenuButton];
        for (int i = 0; i < buttons.Length; i++)
        {
            bool focused = _layer == Layer.TopBar && i == _topBarIndex;
            buttons[i].Background  = new SolidColorBrush(Color.FromArgb(focused ? (byte)80  : (byte)26, _accent.R, _accent.G, _accent.B));
            buttons[i].BorderBrush = new SolidColorBrush(Color.FromArgb(focused ? (byte)220 : (byte)68, _accent.R, _accent.G, _accent.B));
        }
    }

    void ActivateTopBarItem(int index)
    {
        switch (index)
        {
            case 0: OpenShop();     break;
            case 1: OpenSettings(); break;
            case 2: OpenMenu(); break;
        }
    }
}
