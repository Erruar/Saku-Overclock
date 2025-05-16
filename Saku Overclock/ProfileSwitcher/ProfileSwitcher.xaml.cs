using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Visual = Microsoft.UI.Composition.Visual;

namespace Saku_Overclock.ProfileSwitcher;
public sealed partial class ProfileSwitcher : Window
{
    private static ProfileSwitcher? _instance; 
    private readonly Visual _windowVisual; 
    private static CancellationTokenSource? _hideCts; // Контроль запуска анимации скрытия, чтобы скрытие выполнялось только один раз

    public ProfileSwitcher()
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
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Устанавливаем стиль окна как POPUP (убираем заголовок)
        SetWindowStyle(hwnd);

        // Применяем прозрачность фона
        SetTransparentBackground(hwnd); 

        // Начальное смещение окна (контента)
        _windowVisual.Offset = new Vector3(_windowVisual.Offset.X, 0, _windowVisual.Offset.Z);
    }


    // Устанавливаем стиль окна как POPUP
    private static void SetWindowStyle(IntPtr hwnd)
    {
        const int GWL_STYLE = -16;
        const uint WS_POPUP = 0x80000000;
        const uint WS_VISIBLE = 0x10000000;
        var style = GetWindowLong(hwnd, GWL_STYLE);
        _ = SetWindowLong(hwnd, GWL_STYLE, (int)(style & ~(WS_POPUP | WS_VISIBLE)));
    }

    // Устанавливаем прозрачный фон окна
    private void SetTransparentBackground(IntPtr hwnd)
    {
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TRANSPARENT);
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial IntPtr CreateRoundRectRgn(
        int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    public static IntPtr CreateRoundedRegion(int x, int y, int width, int height, int ellipseWidth, int ellipseHeight)
        => CreateRoundRectRgn(x, y, width, height, ellipseWidth, ellipseHeight);

    public static int GetWindowLong(IntPtr hWnd, int nIndex)
        => GetWindowLongW(hWnd, nIndex);

    public static int SetWindowLong(IntPtr hWnd, int nIndex, int newLong)
        => SetWindowLongW(hWnd, nIndex, newLong);

    public static bool SetLayeredAttributes(IntPtr hwnd, uint key, byte alpha, uint flags)
        => SetLayeredWindowAttributes(hwnd, key, alpha, flags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_TRANSPARENT = 0x00000020; 
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x02;

    public static async void ShowOverlay(Contracts.Services.IThemeSelectorService themeSelectorService, IAppSettingsService settingsService, string profileName, string? profileIcon = null, string? profileDesc = null)
    { 
        if (_instance == null)
        {
            _instance = new ProfileSwitcher();
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
            _instance.mainSource.ImageSource = new BitmapImage(new Uri(themeBackground));
        }
        _instance.mainThemeGrid.Opacity = themeSelectorService.Themes[settingsService.ThemeType].ThemeOpacity;
        _instance.mainOpacity.Opacity = themeSelectorService.Themes[settingsService.ThemeType].ThemeMaskOpacity;
        
        // Подготовка контента
        _instance.ProfileText.Text = profileName; 
        _instance.ProfileIcon.Glyph = profileIcon ?? "\uE718";

        if (profileDesc?.Length > 26)
        {
            profileDesc = profileDesc[..26] + "...";
        }
        _instance.ProfileDesc.Text = profileDesc ?? string.Empty;
        
        if (profileDesc == null)
        {
            _instance.ProfileDesc.Visibility = Visibility.Collapsed;
        }
        else
        { 
            _instance.ProfileDesc.Visibility = Visibility.Visible;
        }
        // Сбрасываем начальные значения для анимации при каждом вызове:
        // Для окна: Scale.Y = 0 (высота 0) и Opacity = 0.
        _instance._windowVisual.Scale = new Vector3(1, 0, 1);
        _instance._windowVisual.Opacity = 0;
        // Для анимации размера dpWindow нужно получить его Composition Visual
        var dpVisual = ElementCompositionPreview.GetElementVisual(_instance.dpWindow);
        // Для dpWindow: Scale.X = 50/300, Scale.Y = 1 (высота фиксирована)
        dpVisual.Scale = new Vector3(50f / 300f, 1, 1);

        // Устанавливаем позицию окна согласно плану
        var pos = _instance.AppWindow.Position;
        _instance.Move(pos.X, 40);

        _instance.Show();
        var compositor = _instance._windowVisual.Compositor;
        await _instance.AnimateShow(compositor);

        // Сбрасываем предыдущий таймер анимации скрытия, если такой был запущен
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

    private async Task AnimateShow(Microsoft.UI.Composition.Compositor compositor)
    {
        // Для анимации размера dpWindow нужно получить его Composition Visual
        var dpVisual = ElementCompositionPreview.GetElementVisual(dpWindow);
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

    private async Task AnimateHide(Microsoft.UI.Composition.Compositor compositor)
    {
        // Получаем Composition Visual для dpWindow
        var dpVisual = ElementCompositionPreview.GetElementVisual(dpWindow);
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
