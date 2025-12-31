using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Wrappers;
using WinRT.Interop;
using Visual = Microsoft.UI.Composition.Visual;

namespace Saku_Overclock.PresetSwitcher;

public sealed partial class PresetSwitcher
{
    private static PresetSwitcher? _instance;
    private readonly Visual _windowVisual;

    /// <summary>
    ///     Контроль запуска анимации скрытия, чтобы скрытие выполнялось только один раз
    /// </summary>
    private static CancellationTokenSource?
        _hideCts;

    public PresetSwitcher()
    {
        InitializeComponent();

        // Элемент для анимации - окно
        _windowVisual = ElementCompositionPreview.GetElementVisual(Content);

        // Настройка окна
        this.SetWindowSize(300, 50);
        this.CenterOnScreen();
        this.SetWindowOpacity(120);
        Content.CanDrag = false;
        ExtendsContentIntoTitleBar = true;
        this.SetIsAlwaysOnTop(true);
        this.SetIsResizable(false);
        this.ToggleWindowStyle(true, WindowStyle.SysMenu);
        var hwnd = WindowNative.GetWindowHandle(this);

        // Устанавливаем стиль окна как POPUP (убираем заголовок)
        WindowAttributesWrapper.SetWindowStyle(hwnd);

        // Применяем прозрачность фона
        WindowAttributesWrapper.SetTransparentBackground(hwnd);

        // Начальное смещение окна (контента)
        _windowVisual.Offset = _windowVisual.Offset with { Y = 0 };
    }

    public static async Task ShowOverlay(IThemeSelectorService themeSelectorService,
        IAppSettingsService settingsService, string presetName, string? presetIcon = null, string? presetDesc = null)
    {
        if (_instance == null)
        {
            _instance = new PresetSwitcher();
        }

        if (_instance.Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = (App.MainWindow.Content as FrameworkElement)!.RequestedTheme;
        }

        var themeBackground = themeSelectorService.Themes[settingsService.ThemeType].ThemeBackground;

        if (settingsService.ThemeType > 2 &&
            !string.IsNullOrEmpty(themeBackground) &&
            (themeBackground.Contains("http") || themeBackground.Contains("appx") || File.Exists(themeBackground)))
        {
            _instance.MainSource.ImageSource = new BitmapImage(new Uri(themeBackground));
        }

        _instance.MainThemeGrid.Opacity = themeSelectorService.Themes[settingsService.ThemeType].ThemeOpacity;
        _instance.MainOpacity.Opacity = themeSelectorService.Themes[settingsService.ThemeType].ThemeMaskOpacity;

        // Подготовка контента
        _instance.PresetText.Text = presetName;
        _instance.PresetIcon.Glyph = presetIcon ?? "\uE718";

        if (presetDesc?.Length > 26)
        {
            presetDesc = presetDesc[..26] + "...";
        }

        _instance.PresetDesc.Text = presetDesc ?? string.Empty;

        if (presetDesc == null || string.IsNullOrEmpty(presetDesc))
        {
            _instance.PresetDesc.Visibility = Visibility.Collapsed;
        }
        else
        {
            _instance.PresetDesc.Visibility = Visibility.Visible;
        }

        // Сбрасываем начальные значения для анимации при каждом вызове:
        // Для окна: Scale.Y = 0 (высота 0) и Opacity = 0.
        _instance._windowVisual.Scale = new Vector3(1, 0, 1);
        _instance._windowVisual.Opacity = 0;
        // Для анимации размера dpWindow нужно получить его Composition Visual
        var dpVisual = ElementCompositionPreview.GetElementVisual(_instance.DpWindow);
        // Для dpWindow: Scale.X = 50/300, Scale.Y = 1 (высота фиксирована)
        dpVisual.Scale = new Vector3(50f / 300f, 1, 1);

        // Устанавливаем позицию окна согласно плану
        var pos = _instance.AppWindow.Position;
        _instance.Move(pos.X, 40);

        _instance.Show();
        var compositor = _instance._windowVisual.Compositor;
        await _instance.AnimateShow(compositor);

        // Сбрасываем предыдущий таймер анимации скрытия, если такой был запущен
        // ReSharper disable once MethodHasAsyncOverload
        _hideCts?.Cancel();
        _hideCts?.Dispose();
        _hideCts = new CancellationTokenSource();

        // Запускаем задержку, после которой будет выполнена hide-анимация (только одна hide-анимация для всех вызовов)
        try
        {
            await Task.Delay(3500, _hideCts.Token); // Задержка показа, затем анимация скрытия 
            await _instance.AnimateHide(compositor);
        }
        catch (TaskCanceledException)
        {
            // Если задержка была отменена новым вызовом ShowOverlay, ничего не делаем
        }
    }

    private async Task AnimateShow(Compositor compositor)
    {
        // Для анимации размера dpWindow нужно получить его Composition Visual
        var dpVisual = ElementCompositionPreview.GetElementVisual(DpWindow);
        _windowVisual.CenterPoint = new Vector3(150, 30, 0);

        // Анимация окна: Scale.Y от 0 до 1 за 1.7 сек (это имитирует увеличение высоты с 0 до 40)
        var windowScaleAnimation = compositor.CreateScalarKeyFrameAnimation();
        windowScaleAnimation.InsertKeyFrame(0f, 0f);
        windowScaleAnimation.InsertKeyFrame(1f, 1f);
        windowScaleAnimation.Duration = TimeSpan.FromSeconds(0.5);
        _windowVisual.StartAnimation("Scale.Y", windowScaleAnimation);

        // Анимация для dpWindow: Scale.X от 50/300 до 1 за 1.7 сек (ширина растёт с 50 до 300)
        var dpScaleAnimation = compositor.CreateScalarKeyFrameAnimation();
        dpScaleAnimation.InsertKeyFrame(0f, 50f / 300f);
        dpScaleAnimation.InsertKeyFrame(1f, 1f);
        dpScaleAnimation.Duration = TimeSpan.FromSeconds(0.5);
        dpVisual.StartAnimation("Scale.X", dpScaleAnimation);

        // Параллельно анимируем появление через Opacity
        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f);
        opacityAnimation.InsertKeyFrame(1f, 1f);
        opacityAnimation.Duration = TimeSpan.FromSeconds(0.5);
        _windowVisual.StartAnimation("Opacity", opacityAnimation);

        await Task.Delay(TimeSpan.FromSeconds(0.5));

        // Гарантируем финальные значения (на случай, если анимация завершилась не идеально):  
        _windowVisual.Scale = new Vector3(1, 1, 1);
        _windowVisual.Opacity = 1;
        dpVisual.Scale = new Vector3(1, 1, 1);
    }

    private async Task AnimateHide(Compositor compositor)
    {
        // Получаем Composition Visual для dpWindow
        var dpVisual = ElementCompositionPreview.GetElementVisual(DpWindow);
        _windowVisual.CenterPoint = new Vector3(150, 30, 0);

        // Останавливаем предыдущие анимации перед запуском новых
        _windowVisual.StopAnimation("Scale.Y");
        dpVisual.StopAnimation("Scale.X");
        _windowVisual.StopAnimation("Opacity");

        // Инверсная анимация для окна: Scale.Y от 1 до 0 за 1.7 сек (сжатие по высоте)
        var windowScaleAnimation = compositor.CreateScalarKeyFrameAnimation();
        windowScaleAnimation.InsertKeyFrame(0f, 1f);
        windowScaleAnimation.InsertKeyFrame(1f, 0f);
        windowScaleAnimation.Duration = TimeSpan.FromSeconds(1.3);
        _windowVisual.StartAnimation("Scale.Y", windowScaleAnimation);

        // Инверсная анимация для dpWindow: Scale.X от 1 до 50/300 за 1.7 сек (сужение по ширине)
        var dpScaleAnimation = compositor.CreateScalarKeyFrameAnimation();
        dpScaleAnimation.InsertKeyFrame(0f, 1f);
        dpScaleAnimation.InsertKeyFrame(0.3f, 1.0f);
        dpScaleAnimation.InsertKeyFrame(1f, 50f / 300f);
        dpScaleAnimation.Duration = TimeSpan.FromSeconds(1.3);
        dpVisual.StartAnimation("Scale.X", dpScaleAnimation);

        // Анимация прозрачности: Opacity от 1 до 0 за 1.7 сек
        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0f, 1f);
        fadeAnimation.InsertKeyFrame(1f, 0f);
        fadeAnimation.Duration = TimeSpan.FromSeconds(1.3);
        _windowVisual.StartAnimation("Opacity", fadeAnimation);

        // Дожидаемся завершения анимации перед скрытием окна
        await Task.Delay(TimeSpan.FromSeconds(1.3));
        this.Hide();
    }
}