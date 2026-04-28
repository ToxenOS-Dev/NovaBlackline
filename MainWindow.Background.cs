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
        var bmp = GetWallpaper(_current);
        WallpaperImage.Source = bmp;

        DimOverlay.Background = bmp != null
            ? new LinearGradientBrush
            {
                StartPoint    = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                EndPoint      = new RelativePoint(0.5, 1, RelativeUnit.Relative),
                GradientStops = new GradientStops
                {
                    new GradientStop(Color.FromArgb(210, 0, 0, 0), 0.00),
                    new GradientStop(Color.FromArgb(80,  0, 0, 0), 0.35),
                    new GradientStop(Color.FromArgb(80,  0, 0, 0), 0.65),
                    new GradientStop(Color.FromArgb(220, 0, 0, 0), 1.00),
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

    void UpdateInfo()
    {
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
    }
}
