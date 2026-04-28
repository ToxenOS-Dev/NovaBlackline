using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Linq;

namespace NovaBlackline;

public partial class MainWindow
{
    void InitSettings()
    {
        _settings =
        [
            new("Accent Color",      ["Yellow", "White", "Cyan", "Red", "Green"], 0, ApplyAccent),
            new("Navigation Speed",  ["Slow", "Normal", "Fast"],                  1, ApplyNavSpeed),
        ];
        _settingValues = _settings.Select(s => s.Default).ToArray();
    }

    void OpenSettings()
    {
        _layer = Layer.Settings;
        _settingIndex = 0;
        SettingsOverlay.IsVisible = true;
        DrawSettingsRows();
    }

    void CloseSettings()
    {
        _layer = Layer.Tiles;
        SettingsOverlay.IsVisible = false;
        UpdateTopBar();
    }

    void DrawSettingsRows()
    {
        SettingsRows.Children.Clear();
        for (int i = 0; i < _settings.Length; i++)
        {
            bool sel     = i == _settingIndex;
            var  setting = _settings[i];
            int  valIdx  = _settingValues[i];

            var row = new Border
            {
                Background      = new SolidColorBrush(Color.FromArgb(sel ? (byte)45 : (byte)0, _accent.R, _accent.G, _accent.B)),
                BorderBrush     = new SolidColorBrush(Color.FromArgb(sel ? (byte)80 : (byte)0, _accent.R, _accent.G, _accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(20, 14),
                Margin          = new Thickness(0, 2),
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

            grid.Children.Add(new TextBlock
            {
                Text              = setting.Label,
                FontSize          = 15,
                Foreground        = sel ? new SolidColorBrush(_accent) : Brushes.White,
                FontWeight        = sel ? FontWeight.Bold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center,
            });

            var valueRow = new StackPanel
            {
                Orientation       = Orientation.Horizontal,
                Spacing           = 14,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(valueRow, 1);

            byte arrowAlpha = sel ? (byte)255 : (byte)60;
            valueRow.Children.Add(new TextBlock
            {
                Text              = "◄",
                FontSize          = 13,
                Foreground        = new SolidColorBrush(Color.FromArgb(arrowAlpha, _accent.R, _accent.G, _accent.B)),
                VerticalAlignment = VerticalAlignment.Center,
            });
            valueRow.Children.Add(new TextBlock
            {
                Text              = setting.Options[valIdx],
                FontSize          = 14,
                Foreground        = Brushes.White,
                MinWidth          = 72,
                TextAlignment     = TextAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            });
            valueRow.Children.Add(new TextBlock
            {
                Text              = "►",
                FontSize          = 13,
                Foreground        = new SolidColorBrush(Color.FromArgb(arrowAlpha, _accent.R, _accent.G, _accent.B)),
                VerticalAlignment = VerticalAlignment.Center,
            });

            grid.Children.Add(valueRow);
            row.Child = grid;
            SettingsRows.Children.Add(row);
        }
    }

    void ChangeSettingValue(int dir)
    {
        var setting = _settings[_settingIndex];
        int newVal  = Math.Clamp(_settingValues[_settingIndex] + dir, 0, setting.Options.Length - 1);
        _settingValues[_settingIndex] = newVal;
        setting.Apply(newVal);
        DrawSettingsRows();
    }

    void ApplyAccent(int idx)
    {
        _accent = AccentColors[idx];
        DrawTiles();
        UpdateTopBar();
        DrawSettingsRows();
    }

    void ApplyNavSpeed(int idx)
    {
        int[] delays = [420, 280, 150];
        _navRepeatMs = delays[idx];
    }
}
