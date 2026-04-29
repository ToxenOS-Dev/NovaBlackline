using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using IOPath      = System.IO.Path;
using IOFile      = System.IO.File;

namespace NovaBlackline;

public partial class MainWindow
{
    Bitmap? GetWallpaper(int index)
    {
        if (_wallpapers.TryGetValue(index, out var cached)) return cached;
        var item = Items[index];

        if (item.WallpaperPath != null && IOFile.Exists(item.WallpaperPath))
        {
            var bmp = new Bitmap(item.WallpaperPath);
            _wallpapers[index] = bmp;
            return bmp;
        }
        foreach (var ext in ImageExtensions)
        {
            string path = IOPath.Combine(WallpaperDir, item.Name.ToLower() + ext);
            if (IOFile.Exists(path))
            {
                var bmp = new Bitmap(path);
                _wallpapers[index] = bmp;
                return bmp;
            }
        }

        if (DefaultWallpaperPath != null && IOFile.Exists(DefaultWallpaperPath))
        {
            var bmp = new Bitmap(DefaultWallpaperPath);
            _wallpapers[index] = bmp;
            return bmp;
        }

        _wallpapers[index] = null;
        return null;
    }

    void UpdateBackground()
    {
        if (Items.Length == 0) return;
        var bmp = GetWallpaper(_current);
        WallpaperImage.Source = bmp;
        UpdateSecondaryBackground();

        DimOverlay.Background = bmp != null
            ? new LinearGradientBrush
            {
                StartPoint    = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(_theme.DimTop,    _theme.WindowBackground.R, _theme.WindowBackground.G, _theme.WindowBackground.B), 0.00),
                    new GradientStop(Color.FromArgb(_theme.DimMiddle, _theme.WindowBackground.R, _theme.WindowBackground.G, _theme.WindowBackground.B), 0.35),
                    new GradientStop(Color.FromArgb(_theme.DimMiddle, _theme.WindowBackground.R, _theme.WindowBackground.G, _theme.WindowBackground.B), 0.65),
                    new GradientStop(Color.FromArgb(_theme.DimBottom, _theme.WindowBackground.R, _theme.WindowBackground.G, _theme.WindowBackground.B), 1.00),
                }
            }
            : new LinearGradientBrush
            {
                StartPoint    = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(65, Items[_current].AccentColor.R,
                                                        Items[_current].AccentColor.G,
                                                        Items[_current].AccentColor.B), 0),
                    new GradientStop(Colors.Black, 0.65),
                }
            };
    }

    void UpdateSecondaryBackground()
    {
        if (_secondaryWallpaperImage == null) return;
        int idx = _current + 4;
        _secondaryWallpaperImage.Source = idx < Items.Length ? GetWallpaper(idx) : null;
    }

    void UpdateInfo()
    {
        if (Items.Length == 0) return;
        ItemNameText.Text = Items[_current].Name.ToUpper();
        ItemDescText.Text = Items[_current].Description;
    }

    void DrawTiles()
    {
        TilesCanvas.Children.Clear();

        double cx = TilesCanvas.Bounds.Width  / 2;
        double cy = TilesCanvas.Bounds.Height * 0.62;
        if (cx <= 0) return;

        for (int pass = 0; pass < 2; pass++)
        for (int i   = 0; i   < Items.Length; i++)
        {
            int  dist = i - _current;
            bool sel  = dist == 0;
            if (pass == 0 && sel)  continue;
            if (pass == 1 && !sel) continue;

            int abs = System.Math.Abs(dist);
            if (abs >= TileSizes.Length) continue;

            double size = TileSizes[abs];
            double x    = cx + TileOffsets[abs] * System.Math.Sign(dist) - size / 2;
            double y    = cy - size / 2 - (sel ? 30 : 0);

            var tile = new Border
            {
                Width           = size,
                Height          = size,
                Background      = new SolidColorBrush(Items[i].AccentColor),
                CornerRadius    = new CornerRadius(10),
                Opacity         = TileOpacity[abs],
                BorderThickness = sel ? new Thickness(3) : new Thickness(0),
                BorderBrush     = new SolidColorBrush(_accent),
                Child = new TextBlock
                {
                    Text                = Items[i].Icon,
                    FontSize            = sel ? 76 : size * 0.37,
                    FontWeight          = FontWeight.Bold,
                    Foreground          = sel ? new SolidColorBrush(_accent) : Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Center,
                }
            };

            Canvas.SetLeft(tile, x);
            Canvas.SetTop(tile, y);
            TilesCanvas.Children.Add(tile);
        }

        DrawSecondaryTiles();
    }

    void DrawSecondaryTiles()
    {
        var canvas = _secondaryTilesCanvas;
        if (canvas == null) return;

        canvas.Children.Clear();

        double cw = canvas.Bounds.Width;
        double ch = canvas.Bounds.Height;
        if (cw <= 0 || ch <= 0) return;

        const double tileSize = 80;
        const double step     = tileSize + 16;
        double cy = ch * 0.62 - tileSize / 2;
        double x  = 48;

        for (int i = _current + 4; i < Items.Length && x <= cw; i++, x += step)
        {
            double opacity = System.Math.Max(0.10, 0.50 - (i - _current - 4) * 0.06);

            var tile = new Border
            {
                Width        = tileSize,
                Height       = tileSize,
                Background   = new SolidColorBrush(Items[i].AccentColor),
                CornerRadius = new CornerRadius(10),
                Opacity      = opacity,
                Child = new TextBlock
                {
                    Text                = Items[i].Icon,
                    FontSize            = tileSize * 0.37,
                    FontWeight          = FontWeight.Bold,
                    Foreground          = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment   = Avalonia.Layout.VerticalAlignment.Center,
                }
            };

            Canvas.SetLeft(tile, x);
            Canvas.SetTop(tile, cy);
            canvas.Children.Add(tile);
        }
    }
}
