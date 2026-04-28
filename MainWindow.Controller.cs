using Avalonia.Threading;
using IOPath      = System.IO.Path;
using IOFile      = System.IO.File;
using IODirectory = System.IO.Directory;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NovaBlackline;

public partial class MainWindow
{
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
            if (_shopInContent) MoveShopContentUp();
            return;
        }
        if (_layer == Layer.Settings && _settingIndex > 0) { _settingIndex--; DrawSettingsRows(); return; }
        if (_layer == Layer.Tiles)                         { _layer = Layer.TopBar; _topBarIndex = 0; UpdateTopBar(); }
    }

    void OnControllerDown()
    {
        if (_layer == Layer.Shop)
        {
            if (_shopInContent) MoveShopContentDown();
            else if (ShopTabs[_shopTabIndex].Name == "Nova Shop") EnterShopContent();
            return;
        }
        if (_layer == Layer.Settings && _settingIndex < _settings.Length - 1) { _settingIndex++; DrawSettingsRows(); return; }
        if (_layer == Layer.TopBar)                                           { _layer = Layer.Tiles; UpdateTopBar(); }
    }

    void OnControllerEnter()
    {
        if (_animating) { SkipAnimation(); return; }
        if (_layer == Layer.Shop)
        {
            if (_shopInContent) LaunchFeaturedGame(_shopGameIndex);
            else if (ShopTabs[_shopTabIndex].Name == "Nova Shop") EnterShopContent();
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
}
