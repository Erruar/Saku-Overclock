using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections; 

namespace Saku_Overclock.ProfileSwitcher; 
public sealed partial class ProfileSwitcher : Window
{
    private static ProfileSwitcher? _instance;
    private readonly DispatcherTimer _timer;
    private readonly Visual _windowVisual; 

    public enum DWM_WINDOW_CORNER_PREFERENCE
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }

    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_WINDOW_CORNER_PREFERENCE = 33,
        DWMWA_TRANSITIONS_FORCEDISABLED = 3,
        DWMWA_CLOAK = 13,
        DWMWA_CLOAKED = 14
    }

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern long DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref DWM_WINDOW_CORNER_PREFERENCE pvAttribute, uint cbAttribute);

    public ProfileSwitcher()
    {
        InitializeComponent(); 
        _windowVisual = ElementCompositionPreview.GetElementVisual(Content);
        _timer = new DispatcherTimer()
        {
            
        };
        _timer.Interval = new TimeSpan(0,0,0,1,500);
        this.SetWindowSize(400, 200);  
        this.SystemBackdrop = new MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
        };
        this.CenterOnScreen();
        this.SetWindowOpacity(120);
        Content.CanDrag = false;
        ExtendsContentIntoTitleBar = true;
        this.SetIsAlwaysOnTop(true);
        this.SetIsResizable(false);
        this.ToggleWindowStyle(true, WindowStyle.SysMenu);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SetWindowCornerPreference(hwnd, DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);  //Закруглить окно на Windows 11
        SetTransparentAndClickThrough(hwnd); // Сделать так, чтобы можно было кликать сквозь это окно и оно имело прозрачность
    } 
    private static void SetWindowCornerPreference(IntPtr hwnd, DWM_WINDOW_CORNER_PREFERENCE preference) 
    {
        DwmSetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(uint));
    } 
    private static void SetTransparentAndClickThrough(IntPtr hwnd)
    {
        var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW);
        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x02;


    public static async void ShowOverlay(string profileName, BitmapImage? profileImage = null, FrameworkElement? newGrid = null)
    {
        if (_instance == null)
        {
            _instance = new ProfileSwitcher();
            _instance.InitializeComponent();
        }

        // Подготовить контент до показа окна
        _instance.ProfileText.Text = profileName;
        if (profileImage != null)
        {
            _instance.ProfileImage.Source = profileImage;
        }

        if (newGrid != null)
        {
            _instance.ProfileImage.Visibility = Visibility.Collapsed;
            _instance.SourceGrid.Visibility = Visibility.Visible;
            _instance.SourceGrid.Children.Clear();
            _instance.SourceGrid.Children.Add(newGrid);
        }
        else
        {
            _instance.ProfileImage.Visibility = Visibility.Visible;
            _instance.SourceGrid.Visibility = Visibility.Collapsed;
        }

        // Убедиться, что контент загружен (например, если используются ресурсы, которые требуют времени)
        await Task.Delay(50); // Небольшая задержка для полной прогрузки контента

        // Теперь показываем окно с контентом
        _instance.Show();
        _instance.SetWindowOpacity(155);
        _instance._windowVisual.Opacity = 1;
        _instance._timer?.Stop();


        // Первый таймер на 1,5 секунды
        var displayTimer = new DispatcherTimer()
        {
            Interval = new TimeSpan(0, 0, 0, 2, 500)
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
    }
}
