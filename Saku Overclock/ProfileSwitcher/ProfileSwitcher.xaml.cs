using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Windows.UI.Composition;
using Visual = Microsoft.UI.Composition.Visual;

namespace Saku_Overclock.ProfileSwitcher;
public sealed partial class ProfileSwitcher : Window
{
    private static ProfileSwitcher? _instance; 
    private readonly Visual _windowVisual;

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
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
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
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x02;

    public static async void ShowOverlay(string profileName, string? profileIcon = null, string? profileDesc = null)
    {
        if (_instance == null)
        {
            _instance = new ProfileSwitcher();
        }

        // Подготовка контента
        _instance.ProfileText.Text = profileName;
        _instance.ProfileIcon.Glyph = profileIcon ?? "\uE709";
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

        // Задержка показа, затем анимация скрытия
        await Task.Delay(3500);
        await _instance.AnimateHide(compositor);
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
        windowScaleAnimation.Duration = TimeSpan.FromSeconds(0.9);
        _windowVisual.StartAnimation("Scale.Y", windowScaleAnimation);
        // Анимация для dpWindow: Scale.X от 50/300 до 1 за 1.7 сек (ширина растёт с 50 до 300)
        var dpScaleAnimation = compositor.CreateScalarKeyFrameAnimation();
        dpScaleAnimation.InsertKeyFrame(0f, 50f / 300f);
        dpScaleAnimation.InsertKeyFrame(1f, 1f);
        dpScaleAnimation.Duration = TimeSpan.FromSeconds(0.9);
        dpVisual.StartAnimation("Scale.X", dpScaleAnimation);
        // Параллельно анимируем появление через Opacity
        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, 0f);
        opacityAnimation.InsertKeyFrame(1f, 1f);
        opacityAnimation.Duration = TimeSpan.FromSeconds(0.9);
        _windowVisual.StartAnimation("Opacity", opacityAnimation);

        await Task.Delay(TimeSpan.FromSeconds(0.9));

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
        windowScaleAnimation.Duration = TimeSpan.FromSeconds(1.7);
        _windowVisual.StartAnimation("Scale.Y", windowScaleAnimation);

        // Инверсная анимация для dpWindow: Scale.X от 1 до 50/300 за 1.7 сек (сужение по ширине)
        var dpScaleAnimation = compositor.CreateScalarKeyFrameAnimation();
        dpScaleAnimation.InsertKeyFrame(0f, 1f);
        dpScaleAnimation.InsertKeyFrame(0.3f, 1.0f);
        dpScaleAnimation.InsertKeyFrame(1f, 50f / 300f);
        dpScaleAnimation.Duration = TimeSpan.FromSeconds(1.7);
        dpVisual.StartAnimation("Scale.X", dpScaleAnimation);

        // Анимация прозрачности: Opacity от 1 до 0 за 1.7 сек
        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0f, 1f);
        fadeAnimation.InsertKeyFrame(1f, 0f);
        fadeAnimation.Duration = TimeSpan.FromSeconds(1.7);
        _windowVisual.StartAnimation("Opacity", fadeAnimation);

        // Дожидаемся завершения анимации перед скрытием окна
        await Task.Delay(TimeSpan.FromSeconds(1.7));
        this.Hide();
    }

    /*public static async void ShowOverlay(string profileName, string? profileIcon = null, FrameworkElement? newGrid = null)
    {
        if (_instance == null)
        {
            _instance = new ProfileSwitcher();
        }

        // Подготовка контента
        _instance.ProfileText.Text = profileName;
        _instance.ProfileIcon.Glyph = profileIcon ?? "\uE709";

        if (newGrid != null)
        {
            _instance.SourceGrid.Width = 100;
            _instance.SourceGrid.Height = 100;
            _instance.ProfileIcon.Visibility = Visibility.Collapsed;
            _instance.SourceGrid.Visibility = Visibility.Visible;
            _instance.SourceGrid.Children.Clear();
            _instance.SourceGrid.Children.Add(newGrid);
        }
        else
        {
            _instance.ProfileIcon.Visibility = Visibility.Visible;
            _instance.SourceGrid.Visibility = Visibility.Collapsed;
        }

        _instance.Show();
        var compositor = _instance._windowVisual.Compositor;
        await _instance.AnimateShow(compositor);

        // Задержка показа, затем анимация скрытия
        await Task.Delay(3500);
        await _instance.AnimateHide(compositor);
    }*/
    /*    public static async void ShowOverlay(string profileName, string? profileIcon = null, FrameworkElement? newGrid = null)
        {
            if (_instance == null)
            {
                _instance = new ProfileSwitcher();
                _instance.InitializeComponent();
            }

            // Подготовить контент до показа окна
            _instance.ProfileText.Text = profileName;
            if (profileIcon != null)
            {
                _instance.ProfileIcon.Glyph = profileIcon;
            }
            else
            {
                _instance.ProfileIcon.Glyph = "\uE709";
            }

            if (newGrid != null)
            {
                _instance.SourceGrid.Width = 100;
                _instance.SourceGrid.Height = 100;
                _instance.ProfileIcon.Visibility = Visibility.Collapsed;
                _instance.SourceGrid.Visibility = Visibility.Visible;
                _instance.SourceGrid.Children.Clear();
                _instance.SourceGrid.Children.Add(newGrid);
            }
            else
            {
                _instance.ProfileIcon.Visibility = Visibility.Visible;
                _instance.SourceGrid.Visibility = Visibility.Collapsed;
            }

            // Убедиться, что контент загружен (например, если используются ресурсы, которые требуют времени)
            await Task.Delay(50); // Небольшая задержка для полной прогрузки контента

            // Теперь показываем окно с контентом
            _instance.Show();
            _instance.SetWindowOpacity(255);
            _instance._windowVisual.Opacity = 1;
            _instance._timer?.Stop();


            // Первый таймер на 1,5 секунды
            var displayTimer = new DispatcherTimer()
            {
                Interval = new TimeSpan(0, 0, 0, 3, 500)
            };

            displayTimer.Tick += (s, e) =>
            {
                displayTimer.Stop();

                // Анимация плавного уменьшения прозрачности
                _instance.DispatcherQueue.TryEnqueue(() =>
                {
                    var compositor = _instance._windowVisual.Compositor;
                    var fadeOutAnimation = compositor.CreateScalarKeyFrameAnimation();
                    fadeOutAnimation.InsertKeyFrame(1f, 0f); // Уменьшить прозрачность до 0
                    fadeOutAnimation.Duration = TimeSpan.FromSeconds(1); // За 1 секунду
                    _instance._windowVisual.StartAnimation(nameof(_instance._windowVisual.Opacity), fadeOutAnimation);
                    // Таймер для ожидания завершения анимации перед скрытием фона
                    var hideTimer = new DispatcherTimer
                    {
                        Interval = new TimeSpan(0, 0, 0, 1) // 1 секунда, совпадает с длительностью анимации
                    };
                    hideTimer.Tick += (sender, args) =>
                    {
                        hideTimer.Stop();
                        _instance.SetWindowOpacity(0); // Скрыть фон
                    };
                    hideTimer.Start();
                });
            };

            displayTimer.Start();
        }*/

    /* private async Task AnimateShow(Microsoft.UI.Composition.Compositor compositor)
     {
         var pos = AppWindow.Position;
         this.Move(pos.X, 0);
         // Анимация смещения окна (контента) по оси Y от 0 до 40
         var offsetAnimation = compositor.CreateVector3KeyFrameAnimation();
         offsetAnimation.InsertKeyFrame(0f, _windowVisual.Offset);
         offsetAnimation.InsertKeyFrame(1f, new Vector3(_windowVisual.Offset.X, 40f, _windowVisual.Offset.Z));
         offsetAnimation.Duration = TimeSpan.FromSeconds(1);
         _windowVisual.StartAnimation("Offset", offsetAnimation);

         // Анимация прозрачности окна от 0 до 1
         var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
         opacityAnimation.InsertKeyFrame(0f, 0f);
         opacityAnimation.InsertKeyFrame(1f, 1f);
         opacityAnimation.Duration = TimeSpan.FromSeconds(1);
         _windowVisual.StartAnimation("Opacity", opacityAnimation);

         // Для анимации размера dpWindow нужно получить его Composition Visual
         var dpVisual = ElementCompositionPreview.GetElementVisual(dpWindow);
         // Определяем начальный и конечный размеры. Если ActualWidth ещё не определён, задаём фиксированное значение.
         var width = (float)(dpWindow.ActualWidth == 0 ? 300 : dpWindow.ActualWidth);
         var initialSize = new Vector2(width, 50f);
         var finalSize = new Vector2(width, 300f);

         var sizeAnimation = compositor.CreateVector2KeyFrameAnimation();
         sizeAnimation.InsertKeyFrame(0f, initialSize);
         sizeAnimation.InsertKeyFrame(1f, finalSize);
         sizeAnimation.Duration = TimeSpan.FromSeconds(1);
         dpVisual.StartAnimation("Size", sizeAnimation);

         // Ждём завершения анимаций
         await Task.Delay(1000);
     }*/
    /*private async Task AnimateShow(Microsoft.UI.Composition.Compositor compositor)
    {
        var pos = AppWindow.Position;
        this.Move(pos.X, 0);
        // Получаем Composition Visual для dpWindow (Grid)
        Visual dpVisual = ElementCompositionPreview.GetElementVisual(dpWindow);

        // Анимация для окна:
        // Изменяем Size от (300, 0) до (300, 40)
        Vector2 windowStartSize = new Vector2(300, 0);
        Vector2 windowEndSize = new Vector2(300, 40);
        var windowSizeAnimation = compositor.CreateVector2KeyFrameAnimation();
        windowSizeAnimation.InsertKeyFrame(0f, windowStartSize);
        windowSizeAnimation.InsertKeyFrame(1f, windowEndSize);
        windowSizeAnimation.Duration = TimeSpan.FromSeconds(1.7);

        // Анимация для dpWindow:
        // Изменяем Size от (50, 50) до (300, 50) – высота остаётся 50
        Vector2 dpStartSize = new Vector2(50, 50);
        Vector2 dpEndSize = new Vector2(300, 50);
        var dpSizeAnimation = compositor.CreateVector2KeyFrameAnimation();
        dpSizeAnimation.InsertKeyFrame(0f, dpStartSize);
        dpSizeAnimation.InsertKeyFrame(1f, dpEndSize);
        dpSizeAnimation.Duration = TimeSpan.FromSeconds(1.7);

        // Запускаем анимации параллельно
        _windowVisual.StartAnimation("Size", windowSizeAnimation);
        dpVisual.StartAnimation("Size", dpSizeAnimation);

        // Ждем завершения анимации
        await Task.Delay(TimeSpan.FromSeconds(1.7));
    }*/
    /* private async Task AnimateShow(Microsoft.UI.Composition.Compositor compositor)
     {
         var pos = AppWindow.Position;
         this.Move(pos.X, 0);
         Visual dpVisual = ElementCompositionPreview.GetElementVisual(dpWindow);
         // Анимация окна: Scale.Y от 0 до 1 за 1.7 сек
         var windowAnimation = compositor.CreateScalarKeyFrameAnimation();
         windowAnimation.InsertKeyFrame(0f, 0f);
         windowAnimation.InsertKeyFrame(1f, 1f);
         windowAnimation.Duration = TimeSpan.FromSeconds(1.7);
         _windowVisual.StartAnimation("Scale.Y", windowAnimation);

         // Анимация для dpWindow: Scale.X от 50/300 (~0.1667) до 1 за 1.7 сек
         var dpAnimation = compositor.CreateScalarKeyFrameAnimation();
         dpAnimation.InsertKeyFrame(0f, 50f / 300f);
         dpAnimation.InsertKeyFrame(1f, 1f);
         dpAnimation.Duration = TimeSpan.FromSeconds(1.7);
         dpVisual.StartAnimation("Scale.X", dpAnimation);

         await Task.Delay(1700);
     }

     private async Task AnimateHide(Microsoft.UI.Composition.Compositor compositor)
     {
         // Анимация исчезания: прозрачность от 1 до 0
         var fadeOutAnimation = compositor.CreateScalarKeyFrameAnimation();
         fadeOutAnimation.InsertKeyFrame(0f, 1f);
         fadeOutAnimation.InsertKeyFrame(1f, 0f);
         fadeOutAnimation.Duration = TimeSpan.FromSeconds(1);
         _windowVisual.StartAnimation("Opacity", fadeOutAnimation);

         await Task.Delay(1000);
         this.Hide();
     }*/
}
