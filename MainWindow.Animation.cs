using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NovaBlackline;

public partial class MainWindow
{
    async void PlayStartAnimation()
    {
        _animCts = new CancellationTokenSource();
        var ct   = _animCts.Token;

        StartLogoGroup.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(900),  Easing = new CubicEaseOut() }
        };
        StartTagline.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(700),  Easing = new CubicEaseOut() }
        };
        StartOverlay.Transitions = new Transitions
        {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(1000), Easing = new CubicEaseIn()  }
        };

        try
        {
            StartLogoGroup.Opacity = 1.0;
            await Task.Delay(900 + 900, ct);

            StartTagline.Opacity = 0.45;
            await Task.Delay(700 + 1600, ct);

            StartOverlay.Opacity = 0.0;
            await Task.Delay(1000, ct);
        }
        catch (OperationCanceledException) { }

        FinishAnimation();
    }

    void SkipAnimation()
    {
        if (!_animating) return;
        _animCts?.Cancel();
    }

    void FinishAnimation()
    {
        StartLogoGroup.Transitions = null;
        StartTagline.Transitions   = null;
        StartOverlay.Transitions   = null;
        _animating = false;
        _animCts?.Dispose();
        _animCts = null;
        StartOverlay.IsVisible = false;
    }
}
