using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Helpers;

public sealed class BlurredBackgroundManager : IDisposable
{
    private const float MaxBlurSigma   = 28f;   // при Opacity = 1.0
    private const float BlurTransition = 250f;  // мс, плавная смена блюр эффекта

    private readonly Compositor            _compositor;
    private readonly FrameworkElement      _host;
    private readonly SpriteVisual          _sprite;
    private readonly CompositionEffectBrush _effectBrush;
    private readonly CompositionSurfaceBrush _surfaceBrush;

    private LoadedImageSurface? _surface;
    private bool _disposed;

    public BlurredBackgroundManager(FrameworkElement host)
    {
        _host       = host;
        _compositor = ElementCompositionPreview.GetElementVisual(host).Compositor;

        // GaussianBlur effect с анимируемым параметром
        var blurEffect = new GaussianBlurEffect
        {
            Name         = "Blur",
            BlurAmount   = 0f,
            BorderMode   = EffectBorderMode.Hard,
            Optimization = EffectOptimization.Speed,
            Source       = new CompositionEffectSourceParameter("Image")
        };

        var factory = _compositor.CreateEffectFactory(
            // ReSharper disable once UseCollectionExpression
            // ReSharper disable once RedundantExplicitArrayCreation
            blurEffect, new string[] { "Blur.BlurAmount" });

        _effectBrush = factory.CreateBrush();

        // Surface brush — сюда пишем изображение
        _surfaceBrush = _compositor.CreateSurfaceBrush();
        _surfaceBrush.Stretch = CompositionStretch.UniformToFill;
        _surfaceBrush.HorizontalAlignmentRatio = 0.5f;
        _surfaceBrush.VerticalAlignmentRatio   = 0.5f;
        _effectBrush.SetSourceParameter("Image", _surfaceBrush);

        // SpriteVisual поверх хоста
        _sprite       = _compositor.CreateSpriteVisual();
        _sprite.Brush = _effectBrush;
        SyncSize();

        ElementCompositionPreview.SetElementChildVisual(_host, _sprite);
        _host.SizeChanged += OnSizeChanged;
    }


    /// <summary>Загружает новое изображение по URI (ms-appx:///, http/https, ms-appdata:///)</summary>
    public void SetImage(Uri? imageUri)
    {
        ThrowIfDisposed();

        var old = _surface;

        if (imageUri is not null)
        {
            _surface               = LoadedImageSurface.StartLoadFromUri(imageUri);
            _surfaceBrush.Surface  = _surface;
        }
        else
        {
            _surface              = null;
            _surfaceBrush.Surface = null;
        }

        old?.Dispose();
    }

    /// <summary>
    ///     Устанавливает силу блюр эффекта
    ///     <paramref name="opacity"/> соответствует бывшему ThemeMaskOpacity Opacity:
    ///     0.0 = нет блюр эффекта, 1.0 = максимальный блюр.
    ///     Переход плавный.
    /// </summary>
    public void SetBlurAmount(double opacity)
    {
        ThrowIfDisposed();

        var target = (float)(Math.Clamp(opacity, 0.0, 1.0) * MaxBlurSigma);

        var anim = _compositor.CreateScalarKeyFrameAnimation();
        anim.InsertKeyFrame(1f, target);
        anim.Duration = TimeSpan.FromMilliseconds(BlurTransition);
        _effectBrush.Properties.StartAnimation("Blur.BlurAmount", anim);
    }

    /// <summary>Немедленно (без анимации) сбросить блюр — например при скрытии фона.</summary>
    public void ResetBlur()
    {
        _effectBrush.Properties.InsertScalar("Blur.BlurAmount", 0f);
    }


    private void SyncSize()
    {
        _sprite.Size = new Vector2(
            (float)_host.ActualWidth,
            (float)_host.ActualHeight);
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        => _sprite.Size = new Vector2((float)e.NewSize.Width, (float)e.NewSize.Height);

    private void ThrowIfDisposed()
    {
        if (!_disposed) return;
        throw new ObjectDisposedException(nameof(BlurredBackgroundManager));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _host.SizeChanged -= OnSizeChanged;
        ElementCompositionPreview.SetElementChildVisual(_host, null);

        _surface?.Dispose();
        _effectBrush.Dispose();
        _surfaceBrush.Dispose();
        _sprite.Brush = null;
    }
}