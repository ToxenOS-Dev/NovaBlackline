using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NovaBlackline;

public partial class MainWindow
{
    void ApplyPrimaryDisplayOnly(int idx)
    {
        _primaryDisplayOnly = idx == 0;
        UpdateSecondaryDisplayWindow();
    }

    void ApplyDisplayPlacement()
    {
        ApplyPrimaryDisplayPlacement();
        UpdateSecondaryDisplayWindow();
    }

    void ApplyPrimaryDisplayPlacement()
    {
        if (Screens.ScreenCount < 2)
            return;

        var primary = GetPrimaryScreen();
        if (primary == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            WindowState = WindowState.Normal;
            Position = primary.Bounds.Position;
            Width = primary.Bounds.Width / primary.Scaling;
            Height = primary.Bounds.Height / primary.Scaling;
            WindowState = WindowState.FullScreen;
        });
    }

    Screen? GetPrimaryScreen() => Screens.Primary ?? Screens.All.FirstOrDefault(s => s.IsPrimary);

    Screen? GetSecondaryScreen()
    {
        var primary = GetPrimaryScreen();
        if (primary == null)
            return null;

        return Screens.All.FirstOrDefault(screen => !screen.Equals(primary));
    }

    void UpdateSecondaryDisplayWindow()
    {
        if (_primaryDisplayOnly || Screens.ScreenCount < 2)
        {
            CloseSecondaryDisplayWindow();
            return;
        }

        if (!_mainWindowOpened)
            return;

        var secondary = GetSecondaryScreen();
        if (secondary == null)
        {
            CloseSecondaryDisplayWindow();
            return;
        }

        void ShowSecondaryWindow()
        {
            var window = EnsureSecondaryDisplayWindow();
            PlaceWindowOnScreen(window, secondary);

            if (!window.IsVisible)
                window.Show();

            UpdateSecondaryBackground();
            Activate();
        }

        if (Dispatcher.UIThread.CheckAccess())
            ShowSecondaryWindow();
        else
            Dispatcher.UIThread.Post(ShowSecondaryWindow);
    }

    Window EnsureSecondaryDisplayWindow()
    {
        if (_secondaryDisplayWindow != null)
            return _secondaryDisplayWindow;

        var wallpaper = new Image
        {
            Stretch             = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            Opacity             = 0.45,
            Effect              = new BlurEffect { Radius = 22 },
        };
        _secondaryWallpaperImage = wallpaper;

        var dimOverlay = new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint    = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(0xCC, 0, 0, 0), 0.00),
                    new GradientStop(Color.FromArgb(0x55, 0, 0, 0), 0.40),
                    new GradientStop(Color.FromArgb(0x55, 0, 0, 0), 0.60),
                    new GradientStop(Color.FromArgb(0xCC, 0, 0, 0), 1.00),
                }
            }
        };

        var bottomScrim = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Height            = 320,
            Background        = new LinearGradientBrush
            {
                StartPoint    = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 0.00),
                    new GradientStop(Color.FromArgb(0xBB, 0, 0, 0), 0.55),
                    new GradientStop(Color.FromArgb(0xFF, 0, 0, 0), 1.00),
                }
            }
        };

        var leftVignette = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Width               = 200,
            Background          = new LinearGradientBrush
            {
                StartPoint    = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(0xDD, 0, 0, 0), 0.0),
                    new GradientStop(Color.FromArgb(0x00, 0, 0, 0), 1.0),
                }
            }
        };

        var canvas = new Canvas();
        canvas.SizeChanged += (_, _) => DrawSecondaryTiles();
        _secondaryTilesCanvas = canvas;

        var grid = new Grid();
        grid.Children.Add(wallpaper);
        grid.Children.Add(dimOverlay);
        grid.Children.Add(bottomScrim);
        grid.Children.Add(leftVignette);
        grid.Children.Add(canvas);

        _secondaryDisplayWindow = new Window
        {
            Background         = Brushes.Black,
            Content            = grid,
            Cursor             = new Cursor(StandardCursorType.None),
            ShowInTaskbar      = false,
            WindowDecorations  = WindowDecorations.None,
            Topmost            = true,
            WindowState        = WindowState.Normal,
            Opacity            = _animating ? 0.0 : 1.0,
        };

        return _secondaryDisplayWindow;
    }

    void CloseSecondaryDisplayWindow()
    {
        void CloseWindow()
        {
            _secondaryTilesCanvas    = null;
            _secondaryWallpaperImage = null;
            _secondaryDisplayWindow?.Close();
            _secondaryDisplayWindow  = null;
        }

        if (Dispatcher.UIThread.CheckAccess())
            CloseWindow();
        else
            Dispatcher.UIThread.Post(CloseWindow);
    }

    void PlaceWindowOnScreen(Window window, Screen screen)
    {
        window.WindowState = WindowState.Normal;
        window.Position = screen.Bounds.Position;
        window.Width = screen.Bounds.Width / screen.Scaling;
        window.Height = screen.Bounds.Height / screen.Scaling;
        window.WindowState = WindowState.FullScreen;
    }

    async void PlaySecondaryDisplayFade()
    {
        var window = _secondaryDisplayWindow;
        if (window == null)
            return;

        window.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(1000), Easing = new CubicEaseOut() }
        };

        window.Opacity = 1.0;
        await Task.Delay(1000);
        window.Transitions = null;
    }

    string BuildPrimaryDisplayEnvironmentPrefix()
    {
        if (Screens.ScreenCount < 2)
            return "";

        var primary = GetPrimaryScreen();
        if (primary == null)
            return "";

        int displayIndex = GetScreenIndex(primary);
        string displayName = primary.DisplayName ?? "";
        return $"SDL_VIDEO_FULLSCREEN_DISPLAY={displayIndex} " +
               $"GDK_FULLSCREEN_MONITOR={displayIndex} " +
               $"SDL_VIDEO_FULLSCREEN_HEAD={ShellQuote(displayName)} ";
    }

    int GetScreenIndex(Screen screen)
    {
        for (int i = 0; i < Screens.All.Count; i++)
        {
            if (Screens.All[i].Equals(screen))
                return i;
        }

        return 0;
    }
}
