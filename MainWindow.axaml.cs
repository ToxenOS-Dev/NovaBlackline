using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NovaBlackline;

public partial class MainWindow : Window
{
    static readonly double[] TileSizes   = [200, 155, 120, 90];
    static readonly double[] TileOffsets = [0,   265, 430, 560];
    static readonly double[] TileOpacity = [1.0, 0.6, 0.35, 0.18];

    Color _accent = Color.FromRgb(255, 215, 0);
    ThemeProfile _theme = new(Color.FromRgb(0, 0, 0), Color.FromArgb(0xDD, 0, 0, 0), Color.FromRgb(15, 15, 15), 210, 80, 220);

    int   _current;
    Layer _layer       = Layer.Tiles;
    int   _topBarIndex = 0;
    int   _navRepeatMs = 280;

    SettingRow[] _settings = [];
    int[]        _settingValues = [];
    int          _settingIndex  = 0;
    TimeZoneInfo _clockTimeZone = TimeZoneInfo.Local;
    string       _clockFormat   = "HH:mm";
    int          _languageIndex = 0;
    bool         _primaryDisplayOnly = true;

    int  _menuIndex       = 0;

    int  _shopTabIndex    = 0;
    bool _shopInContent   = false;
    int  _shopGameIndex   = 0;

    Timer?                   _clockTimer;
    CancellationTokenSource? _animCts;
    CancellationTokenSource? _switchCts;
    DateTime                 _lastSwitchAt = DateTime.MinValue;
    bool                     _animating = true;
    bool                     _gameSessionActive = false;
    bool                     _mainWindowOpened = false;
    Window?                  _secondaryDisplayWindow;
    Canvas?                  _secondaryTilesCanvas;
    Image?                   _secondaryWallpaperImage;
    readonly HashSet<string>       _monitoredControllers = new();
    readonly Dictionary<int, Bitmap?> _wallpapers        = new();

    public MainWindow()
    {
        InitializeComponent();
        Cursor = new Cursor(StandardCursorType.None);
        InitSettings();
        Opened += (_, _) =>
        {
            _mainWindowOpened = true;
            ApplyDisplayPlacement();
        };
        Screens.Changed += (_, _) => ApplyDisplayPlacement();

        KeyDown += OnKeyDown;
        TilesCanvas.SizeChanged += (_, _) => DrawTiles();

        StoreButton.PointerPressed    += (_, _) => ActivateTopBarItem(0);
        SettingsButton.PointerPressed += (_, _) => ActivateTopBarItem(1);
        MenuButton.PointerPressed     += (_, _) => ActivateTopBarItem(2);

        CloseAppButton.PointerPressed  += (_, _) => Close();
        MenuDimBackground.PointerPressed += (_, _) => CloseMenu();

        _clockTimer = new Timer(
            _ => Dispatcher.UIThread.Post(UpdateClock),
            null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        UpdateClock();
        UpdateInfo();
        UpdateBackground();
        StartControllerMonitor();
        PlayStartAnimation();
    }

    void UpdateClock() => ClockText.Text = TimeZoneInfo.ConvertTime(DateTimeOffset.Now, _clockTimeZone).ToString(_clockFormat);

    void UpdateAll()
    {
        UpdateBackground();
        UpdateInfo();
        DrawTiles();
    }
}
