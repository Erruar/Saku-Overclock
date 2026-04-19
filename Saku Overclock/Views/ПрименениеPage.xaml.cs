using Windows.Foundation.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Views;

public sealed partial class ПрименениеPage
{
    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>(); // Настройки
    private readonly INavigationService _navigationService = App.GetService<INavigationService>(); // Навигация

    private bool _isLoaded; // Состояние загрузки

    public ПрименениеPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
        Loaded += OnLoaded;
    }

    #region Init

    /// <summary>
    ///     Загружает тексты в верхний бар навигации
    /// </summary>
    private void SetupBreadcrumb()
    {
        PageBreadcrumb.ItemsSource = new List<string>
        {
            "Settings_Name/Text".GetLocalized(), // "Настройки" (локализованная строка)
            "Settings_ApplyPage/Text".GetLocalized() // "Параметры применения"
        };
    }

    /// <summary>
    ///     Страница загружена
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        GetSystemInfo.ReadDesignCapacity(out var notTrack);
        if (notTrack)
        {
            AutomationOptions.Visibility = Visibility.Collapsed;
        }
        LoadApplyOptions();
    }

    /// <summary>
    ///     Заполняет левую панель карточками с темами и прокручивает до нужного места активную карточку
    /// </summary>
    private void LoadApplyOptions()
    {
        ApplyStart.IsOn = _appSettings.ReapplyLatestSettingsOnAppLaunch;
        AutoReapply.IsOn = _appSettings.ReapplyOverclock;
    }

    #endregion

    #region Event Handlers

    /// <summary>
    ///     Изменяет состояние пере-применения последних применённых параметров разгона при запуске программы (включены,
    ///     выключены)
    /// </summary>
    private void ApplyOptionsOnStart_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _appSettings.ReapplyLatestSettingsOnAppLaunch = ApplyStart.IsOn;

        _appSettings.SaveSettings();
    }

    /// <summary>
    ///     Изменяет состояние пере-применение последних применённых параметров каждые несколько секунд (включено, выключено)
    /// </summary>
    private void AutoReapplyOptionsEverySeconds_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _appSettings.ReapplyOverclock = AutoReapply.IsOn;

        _appSettings.ReapplyOverclockTimer = 3;

        _appSettings.SaveSettings();
    }

    #endregion

    #region Reset Options
    
    private async void ResetAllOptions_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var confirm = new ContentDialog
            {
                Title = "Settings_Apply_Reset_All_Title".GetLocalized(),
                Content = "Settings_Apply_Reset_All_Desc".GetLocalized(),
                PrimaryButtonText = "ThemeResetAction/Text".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                confirm.XamlRoot = XamlRoot;

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            _appSettings.ReapplyLatestSettingsOnAppLaunch = true;
            _appSettings.ReapplyOverclock = true;
            _appSettings.SaveSettings();
            
            LoadApplyOptions();
        }
        catch (Exception exception)
        {
            await LogHelper.LogError(exception);
        }
    }

    #endregion

    #region Helpers
    
    
    /// <summary>
    ///     Изменяет состояние привязанных ToggleSwitch
    /// </summary>
    private void ToggleTheSwitchByTag(object sender, object e)
    {
        if (sender is FrameworkElement { Tag: string targetName })
        {
            // Ищем элемент по имени на текущей странице и меняем его состояние
            var targetToggle = FindName(targetName) as ToggleSwitch;
            if (targetToggle != null)
            {
                targetToggle.IsOn = !targetToggle.IsOn;
            }
        }
    }
    
    private void PageBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0) _navigationService.GoBack();
    }

    #endregion

    private void ChargerComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        
    }

    private void BatteryComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        
    }
}