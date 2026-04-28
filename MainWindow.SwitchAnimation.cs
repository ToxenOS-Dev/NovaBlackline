using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NovaBlackline;

public partial class MainWindow
{
    async void SwitchToItem(int newIndex)
    {
        if (newIndex < 0 || newIndex >= Items.Length || newIndex == _current)
            return;

        var now = DateTime.UtcNow;
        bool fastRepeat = (now - _lastSwitchAt).TotalMilliseconds < 260;
        _lastSwitchAt = now;

        _switchCts?.Cancel();
        _switchCts?.Dispose();
        _switchCts = new CancellationTokenSource();
        var ct = _switchCts.Token;

        int direction = newIndex > _current ? 1 : -1;
        var tilesTransform = EnsureTileTransform();
        TimeSpan duration = TimeSpan.FromMilliseconds(fastRepeat ? 75 : 155);

        ClearSwitchTransitions();
        _current = newIndex;
        UpdateAll();

        tilesTransform.X = (fastRepeat ? 18 : 34) * direction;
        TilesCanvas.Opacity = fastRepeat ? 0.75 : 0.35;
        ItemNameText.Opacity = fastRepeat ? 0.7 : 0;
        ItemDescText.Opacity = fastRepeat ? 0.7 : 0;
        WallpaperImage.Opacity = fastRepeat ? 0.85 : 0.55;

        try
        {
            await Task.Delay(16, ct);
            SetSwitchTransitions(duration, new CubicEaseOut());

            tilesTransform.X = 0;
            TilesCanvas.Opacity = 1;
            ItemNameText.Opacity = 1;
            ItemDescText.Opacity = 1;
            WallpaperImage.Opacity = 1;

            await Task.Delay(duration, ct);
        }
        catch (OperationCanceledException) { }
    }

    TranslateTransform EnsureTileTransform()
    {
        if (TilesCanvas.RenderTransform is TranslateTransform transform)
            return transform;

        transform = new TranslateTransform();
        TilesCanvas.RenderTransform = transform;
        return transform;
    }

    void SetSwitchTransitions(TimeSpan duration, Easing easing)
    {
        TilesCanvas.Transitions =
        [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = easing },
        ];

        EnsureTileTransform().Transitions =
        [
            new DoubleTransition { Property = TranslateTransform.XProperty, Duration = duration, Easing = easing },
        ];

        ItemNameText.Transitions =
        [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = easing },
        ];

        ItemDescText.Transitions =
        [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = easing },
        ];

        WallpaperImage.Transitions =
        [
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = duration, Easing = easing },
        ];
    }

    void ClearSwitchTransitions()
    {
        TilesCanvas.Transitions = null;
        EnsureTileTransform().Transitions = null;
        ItemNameText.Transitions = null;
        ItemDescText.Transitions = null;
        WallpaperImage.Transitions = null;
    }
}
