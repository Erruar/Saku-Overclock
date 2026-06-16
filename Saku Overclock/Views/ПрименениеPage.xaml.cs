using Windows.Foundation.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Views;

public sealed partial class ПрименениеPage
{
    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>(); // Настройки
    private readonly INavigationService _navigationService = App.GetService<INavigationService>(); // Навигация
    private readonly IPresetManagerService _presetManager = App.GetService<IPresetManagerService>(); // Пресеты

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
        GetSystemInfo.ReadDesignCapacity(out var notTrack);
        if (notTrack)
            AutomationOptions.Visibility = Visibility.Collapsed;
        LoadApplyOptions();
        _isLoaded = true;
    }

    /// <summary>
    ///     Заполняет левую панель карточками с темами и прокручивает до нужного места активную карточку
    /// </summary>
    private void LoadApplyOptions()
    {
        ApplyStart.IsOn = _appSettings.ReapplyLatestSettingsOnAppLaunch;
        AutoReapply.IsOn = _appSettings.ReapplyOverclock;
        AutoReapplyGrid.CornerRadius = AutoReapply.IsOn 
            ? new CornerRadius(0) 
            : new  CornerRadius(0,0,15,15);
        ReapplyTimeSet.Value = new PresetOption<double>(_appSettings.ReapplyOverclock, _appSettings.ReapplyOverclockTimer);

        ReapplyTimeSet.ValueChanged += option =>
        {
            _appSettings.ReapplyOverclockTimer = option.Value;
            _appSettings.SaveSettings();
        };
        
        var presetCollection = new List<PresetDisplayItem>
        {
            new()
            {
                Id = string.Empty,
                Name = "Preset_SelectionNone".GetLocalized()
            }
        };

        PresetDisplayItem? selectedBatteryPreset = null;
        PresetDisplayItem? selectedChargerPreset = null;
        foreach (var preset in _presetManager.Presets)
        {
            var presetName = preset.PresetName;
    
            if (presetName.Contains("Preset_")) presetName = ГлавнаяPage.TryLocalize(presetName);

            var currentPreset = new PresetDisplayItem
            {
                Id = preset.PresetId,
                Name = presetName
            };
            presetCollection.Add(currentPreset);
            
            if (!string.IsNullOrWhiteSpace(_appSettings.BatteryPreset) && preset.PresetId == _appSettings.BatteryPreset) selectedBatteryPreset = currentPreset;
            if (!string.IsNullOrWhiteSpace(_appSettings.AcPreset) && preset.PresetId == _appSettings.AcPreset) selectedChargerPreset = currentPreset;
        }

        BatteryComboBox.ItemsSource = presetCollection;
        ChargerComboBox.ItemsSource = presetCollection;

        if (selectedBatteryPreset != null)
            BatteryComboBox.SelectedItem = selectedBatteryPreset;
        else
            BatteryComboBox.SelectedIndex = 0;

        if (selectedChargerPreset != null)
            ChargerComboBox.SelectedItem = selectedChargerPreset;
        else
            ChargerComboBox.SelectedIndex = 0;
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
        if (!_isLoaded) return;

        AutoReapplyGrid.CornerRadius = AutoReapply.IsOn 
            ? new CornerRadius(0) 
            : new  CornerRadius(0,0,15,15);

        _appSettings.ReapplyOverclock = AutoReapply.IsOn;
        _appSettings.ReapplyOverclockTimer = ReapplyTimeSet.Value?.Value ?? 3;
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
        if (!_isLoaded) return;
        
        if (ChargerComboBox.SelectedIndex == BatteryComboBox.SelectedIndex && BatteryComboBox.SelectedIndex != 0) BatteryComboBox.SelectedIndex = 0;
        _appSettings.AcPreset = ((PresetDisplayItem)ChargerComboBox.SelectedItem).Id;
        _appSettings.SaveSettings();
    }

    private void BatteryComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded)  return;

        if (ChargerComboBox.SelectedIndex == BatteryComboBox.SelectedIndex && BatteryComboBox.SelectedIndex != 0) ChargerComboBox.SelectedIndex = 0;
        _appSettings.BatteryPreset = ((PresetDisplayItem)BatteryComboBox.SelectedItem).Id;
        _appSettings.SaveSettings();
    }
}