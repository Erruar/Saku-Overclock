using System.Numerics;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Services;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Task = System.Threading.Tasks.Task;
using VisualTreeHelper = Saku_Overclock.Helpers.VisualTreeHelper;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private static readonly IPresetManagerService PresetManager = App.GetService<IPresetManagerService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 

    private bool
        _presetChanging = true; // Ожидание окончательной смены пресета на другой. Активируется при смене пресета 

    private int _presetIndex; // Выбранный пресет
    private readonly IBackgroundDataUpdater _dataUpdater = App.GetService<IBackgroundDataUpdater>();

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения

    private static readonly ICpuService Cpu = App.GetService<ICpuService>();
    private static bool? _isPlatformPc = false;
    private string _doubleClickApplyToken = string.Empty;

    public ПресетыPage()
    {
        InitializeComponent();

        PresetManager.LoadSettings();
        AppSettings.SaveSettings();

        _dataUpdater.DataUpdated += OnDataUpdated;

        Unloaded += ПресетыPage_Unloaded;
        Loaded += ПресетыPage_Loaded;
    }

    private void ПресетыPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _dataUpdater.DataUpdated -= OnDataUpdated;

        Unloaded -= ПресетыPage_Unloaded;
        Loaded -= ПресетыPage_Loaded;
    }

    private void ПресетыPage_Loaded(object sender, RoutedEventArgs e)
    {
        _presetChanging = false;
        SelectedPresetDescription.Text = "Preset_Min_Desc/Text".GetLocalized();

        try
        {
            _isPlatformPc = Cpu.IsPlatformPc();
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }

        LoadPresets();

        CurveOptimizerCustomGrid.Visibility =
            OcFinder.IsUndervoltingAvailable() ? Visibility.Visible : Visibility.Collapsed;
        if (CurveOptimizerCustomGrid.Visibility == Visibility.Collapsed) CurveOptimizerCustom.IsOn = false;

        _isLoaded = true;
    }

    #region JSON and Initialization

    #region Initialization

    private void LoadPresets()
    {
        // Загрузить пресеты перед началом работы с ними
        PresetManager.LoadSettings();

        // Очистить элементы PresetsControl
        PresetsControl.Items.Clear();

        // Пройтись по каждому пресету и добавить их в PresetsControl
        var isOneSelected = false;
        for (var i = 0; i < PresetManager.Presets.Length; i++)
        {
            var preset = PresetManager.Presets[i];
            var isChecked = AppSettings.Preset != -1 && AppSettings.Preset == i &&
                            PresetManager.Presets[AppSettings.Preset].PresetName == preset.PresetName &&
                            PresetManager.Presets[AppSettings.Preset].PresetDesc == preset.PresetDesc &&
                            PresetManager.Presets[AppSettings.Preset].PresetIcon == preset.PresetIcon;

            if (isChecked) isOneSelected = true;


            var presetName = preset.PresetName;
            var presetDesc = preset.PresetDesc;
            if (presetName.Contains("Preset_")) presetName = ГлавнаяPage.TryLocalize(presetName);

            if (presetDesc.Contains("Preset_")) presetDesc = ГлавнаяPage.TryLocalize(presetDesc);

            var toggleButton = new PresetItem
            {
                IsSelected = isChecked,
                IconGlyph = preset.PresetIcon == string.Empty ? "\uE718" : preset.PresetIcon,
                Text = presetName,
                Description = presetDesc != string.Empty ? presetDesc : presetName
            };
            PresetsControl.Items.Add(toggleButton);
        }

        if ((PresetManager.Presets.Length == 0 && AppSettings.Preset != -1) || !isOneSelected)
        {
            AppSettings.Preset = -1;
            AppSettings.SaveSettings();
        }

        // Workaround чтобы все элементы корректно загрузились в PresetsControl
        PresetsControl.UpdateView();

        foreach (var item in PresetsControl.Items)
            if (item.IsSelected)
            {
                SelectedPresetName.Text = item.Text;

                // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
                SelectedPresetDescription.Text = item.Description != item.Text ? item.Description : string.Empty;
                if (item.Description == item.Text)
                {
                    SelectedPresetDescription.Visibility = Visibility.Collapsed;
                    EditCurrentButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                    EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                    SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                }
                else
                {
                    SelectedPresetDescription.Visibility = Visibility.Visible;
                    EditCurrentButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                    EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                    SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                }
            }

        if (AppSettings.Preset != -1) InitializeCustomPresetSettings(AppSettings.Preset);
    }

    private void InitializeCustomPresetSettings(int index)
    {
        _presetChanging = true;
        if (index > PresetManager.Presets.Length || index < 0)
        {
            _presetChanging = false;
            return;
        }

        try
        {
            // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
            var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(
                1.17335141 * PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value + 0.21631949);

            BaseTdpSlider.Value = PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value;
            if (PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled &&
                PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.IsEnabled &&
                PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled &&
                (int)PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value ==
                (int)PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.Value &&
                (int)PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value == fineTunedTdp)
            {
                AutoTdpSetOnly(AutoTdpEnabled);
            }
            else
            {
                if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
                {
                    // Так как на компьютерах невозможно выставить другие Power лимиты
                    if (!PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled &&
                        !PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled &&
                        (int)PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value == fineTunedTdp)
                        AutoTdpSetOnly(AutoTdpEnabled);
                }
                else
                {
                    if (PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled ||
                        PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.IsEnabled ||
                        PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled)
                    {
                        AutoTdpSetOnly(AutoTdpManual);
                    }
                    else
                    {
                        AutoTdpSetOnly(AutoTdpDisabled);
                    }
                }
            }

            if (IsRavenFamily())
            {
                if (PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled &&
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled &&
                    (int)PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value ==
                    1200)
                {
                    if ((int)PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value ==
                        800)
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;

                    if ((int)PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value ==
                        1000)
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 2;
                }
                else
                {
                    IntegratedGpuEnchantmentCombo.SelectedIndex = 0;
                }
            }
            else
            {
                if (PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled)
                {
                    if ((int)PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value == 1750)
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 1;

                    if ((int)PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value == 2200)
                        IntegratedGpuEnchantmentCombo.SelectedIndex = 2;
                }
                else
                {
                    IntegratedGpuEnchantmentCombo.SelectedIndex = 0;
                }
            }

            if (!PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled &&
                !PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled)
            {
                BetterTurboCombo.SelectedIndex = 0;
            }
            else
            {
                if ((PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled &&
                     !PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled) ||
                    (!PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled &&
                     PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled))
                    BetterTurboCombo.SelectedIndex = 0;

                if (PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled &&
                    PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled)
                {
                    if ((int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value == 400 &&
                        (int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value == 3)
                        BetterTurboCombo.SelectedIndex = 1;
                    else if ((int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value == 5000 &&
                             (int)PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value == 1)
                        BetterTurboCombo.SelectedIndex = 2;
                    else
                        BetterTurboCombo.SelectedIndex = 0;
                }
                else
                {
                    BetterTurboCombo.SelectedIndex = 0;
                }
            }

            if (PresetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled)
            {
                CurveOptimizerCustom.IsOn = true;
                CurveOptimizerLevelCustomSlider.Value = PresetManager.Presets[index].CurveOptimizerOptions
                    .CpuCurveOptimizerUndervoltingLevel.Value;
            }
            else
            {
                CurveOptimizerCustom.IsOn = false;
            }
        }
        catch
        {
            LogHelper.LogError("Preset contains error. Creating new preset.");

            PresetManager.Presets = new Preset[1];
            PresetManager.Presets[0] = new Preset();
            PresetManager.SaveSettings();
        }

        _presetChanging = false;
    }

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TdpLimitSensorText.Text = $"{info.CpuFastLimit:F0}W";
            TdpValueSensorText.Text = $"{info.CpuFastValue:F0}W";
            CpuFreqSensorText.Text = $"{info.CpuFrequency:F1}GHz";


            if (info.ApuTempValue == 0)
                GpuTempSensorStackPanel.Visibility = Visibility.Collapsed;
            else
                GpuTempSensorText.Text = Math.Round(info.ApuTempValue) + "C";

            CpuTempSensorText.Text = Math.Round(info.CpuTempValue) + "C";
        });
    }

    #endregion

    #endregion

    #region Event Handlers

    #region Additional Functions

    private void BaseTdp_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging) return;

        ChangedBaseTdp_Value();
    }

    private void ChangedBaseTdp_Value()
    {
        if (_presetChanging) return;

        var index = _presetIndex == -1 ? 0 : _presetIndex;

        // Заранее скомпилированная функция увеличения TDP, созданная специально для фирменной функции Smart TDP
        var fineTunedTdp = ПараметрыPage.FromValueToUpperFive(1.17335141 * BaseTdpSlider.Value + 0.21631949);

        PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled = true;
        PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.IsEnabled = true;
        PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled = true;
        PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.Value = BaseTdpSlider.Value;
        PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.Value = BaseTdpSlider.Value;

        if (AutoTdpEnabled.IsChecked == true)
            PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value = fineTunedTdp;
        else
            PresetManager.Presets[index].CpuSettings.CpuActualPowerLimit.Value = BaseTdpSlider.Value;

        if (_isPlatformPc != false && SettingsViewModel.VersionId != 5) // Если устройство - не ноутбук
        {
            // Так как на компьютерах невозможно выставить другие Power лимиты
            PresetManager.Presets[index].CpuSettings.CpuSustainedPowerLimit.IsEnabled = false;
            PresetManager.Presets[index].CpuSettings.CpuAveragePowerLimit.IsEnabled = false;
        }

        PresetManager.SaveSettings();
    }

    private void IntegratedGpuEnchantmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging) return;

        if (AppSettings.Preset == -1) return;

        var index = AppSettings.Preset;

        switch (IntegratedGpuEnchantmentCombo.SelectedIndex)
        {
            case 0:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled =
                        false;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled =
                        false;
                }
                else
                {
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = false;
                }

                break;
            case 1:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = 1200;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = 800;
                }
                else
                {
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value = 1750;
                }

                break;
            case 2:
                if (IsRavenFamily())
                {
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MaximumIntegratedGraphicsFrequency.Value = 1200;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].SubsystemsSettings.MinimumIntegratedGraphicsFrequency.Value = 1000;
                }
                else
                {
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = true;
                    PresetManager.Presets[index].FrequenciesSettings.IntegratedGraphicsFrequency.Value = 2200;
                }

                break;
        }

        PresetManager.SaveSettings();
    }

    private void BetterTurboCombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded || _presetChanging) return;

        if (AppSettings.Preset == -1) return;

        var index = AppSettings.Preset;

        switch (BetterTurboCombo.SelectedIndex)
        {
            case 0:
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled = false;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled = false;
                break;
            case 1:
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value = 400;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value = 3;
                break;
            case 2:
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.IsEnabled = true;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeSlow.Value = 5000;
                PresetManager.Presets[index].CpuSettings.CpuBoostTimeFast.Value = 1;
                break;
        }
        
        PresetManager.SaveSettings();
    }
    
    #region Function Helpers

    private bool IsRavenFamily()
    {
        return Cpu.GetCodenameGeneration() == CpuService.CodenameGeneration.Fp5;
    }

    private void TryAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    #endregion

    #region Preset Management

    private async void AddPresetButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await OpenAddPresetDialogAsync();
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async Task OpenAddPresetDialogAsync()
    {
        var selectedGlyph = "\uE718"; // Значок по умолчанию

        // Кнопка выбора иконки
        var glyphIcon = new FontIcon { Glyph = selectedGlyph };
        var iconButton = new Button
        {
            Height = 60,
            Width = 60,
            CornerRadius = new CornerRadius(16),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 14),
            Content = glyphIcon
        };

        // Поля ввода
        var nameBox = new TextBox
        {
            PlaceholderText = "Param_Preset_New_Name_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Text = "Param_UnsignedPreset".GetLocalized(),
            Width = 250,
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var descBox = new TextBox
        {
            PlaceholderText = "Param_Preset_New_Desc_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 250,
            Margin = new Thickness(0, 6, 0, 0),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        // Стек с текст-боксами и кнопкой
        var fieldsPanel = new StackPanel
        {
            Margin = new Thickness(15, 0, 0, 0)
        };
        fieldsPanel.Children.Add(nameBox);
        fieldsPanel.Children.Add(descBox);

        // Основная горизонтальная панель
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15
        };
        content.Children.Add(iconButton);
        content.Children.Add(fieldsPanel);

        // Создаём диалог
        var dialog = new ContentDialog
        {
            Title = "Param_Preset_New_Name/Content".GetLocalized(),
            XamlRoot = XamlRoot,
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            PrimaryButtonText = "Param_Preset_New_Name/Content".GetLocalized(),
            Content = content,
            DefaultButton = ContentDialogButton.Close
        };

        // --- Обработчики ---

        // Обработчик выбора иконки — обновляем иконку при выборе сразу
        ItemClickEventHandler itemClickHandler = (_, e) =>
        {
            var clickedGlyph = (FontIcon)e.ClickedItem;
            if (clickedGlyph != null)
            {
                selectedGlyph = clickedGlyph.Glyph;
                glyphIcon.Glyph = selectedGlyph;
            }
        };

        RoutedEventHandler clickHandler = (_, _) =>
        {
            // Подписываемся на выбор иконки
            SelectionGrid.ItemClick -= itemClickHandler; // Избегаем дублирования
            SelectionGrid.ItemClick += itemClickHandler;

            SymbolFlyout.ShowAt(iconButton);
        };

        iconButton.Click += clickHandler;

        var result = await dialog.ShowAsync();

        // --- Очистка после диалога ---
        iconButton.Click -= clickHandler;
        SelectionGrid.ItemClick -= itemClickHandler;

        if (result == ContentDialogResult.Primary) await AddPreset(nameBox.Text, descBox.Text, selectedGlyph);
    }

    private async Task AddPreset(string presetName, string presetDesc, string glyph)
    {
        if (presetName != "")
        {
            await LogHelper.Log($"Adding new preset: \"{presetName}\"");

            try
            {
                if (PresetManager.Presets.Length != 0)
                {
                    AppSettings.Preset += 1;
                    _presetIndex += 1;
                }

                _presetChanging = true;
                if (PresetManager.Presets.Length == 0)
                {
                    PresetManager.Presets = new Preset[1];
                    PresetManager.Presets[0] = new Preset
                        { PresetName = presetName, PresetDesc = presetDesc, PresetIcon = glyph };
                }
                else
                {
                    var presetList = new List<Preset>(PresetManager.Presets)
                    {
                        new()
                        {
                            PresetName = presetName,
                            PresetDesc = presetDesc,
                            PresetIcon = glyph
                        }
                    };
                    PresetManager.Presets = [.. presetList];
                }

                _presetChanging = false;
                NotificationsService.ShowNotification("SaveSuccessTitle".GetLocalized(),
                    "SaveSuccessDesc".GetLocalized() + " " + presetName,
                    InfoBarSeverity.Success);
            }
            catch
            {
                // Ignored
            }
        }
        else
        {
            NotificationsService.ShowNotification("Add_Target_Error/Title".GetLocalized(),
                "Add_Target_Error/Subtitle".GetLocalized(),
                InfoBarSeverity.Error);
        }

        AppSettings.SaveSettings();
        PresetManager.SaveSettings();
        LoadPresets();
    }

    private void EditPresetButton_Click(string presetName, string presetDesc, string glyph)
    {
        try
        {
            LogHelper.Log(
                $"Editing preset name: From \"{PresetManager.Presets[_presetIndex].PresetName}\" To \"{presetName}\"");
            if (presetName != "")
            {
                PresetManager.Presets[_presetIndex].PresetName = presetName;
                PresetManager.Presets[_presetIndex].PresetDesc = presetDesc;
                PresetManager.Presets[_presetIndex].PresetIcon = glyph;
                PresetManager.SaveSettings();
                _presetChanging = true;
                LoadPresets();
                _presetChanging = false;
                NotificationsService.ShowNotification("Edit_Target/Title".GetLocalized(),
                    "Edit_Target/Subtitle".GetLocalized() + " " + presetName,
                    InfoBarSeverity.Success);
            }
            else
            {
                NotificationsService.ShowNotification("Edit_Target_Error/Title".GetLocalized(),
                    "Edit_Target_Error/Subtitle".GetLocalized(),
                    InfoBarSeverity.Error);
            }
        }
        catch (Exception exception)
        {
            LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void DeletePresetButton_Click()
    {
        try
        {
            await LogHelper.Log("Showing delete preset dialog");
            var delDialog = new ContentDialog
            {
                Title = "Param_DelPreset_Text".GetLocalized(),
                Content = "Param_DelPreset_Desc".GetLocalized(),
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "Delete".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            // Use this code to associate the dialog to the appropriate AppWindow by setting
            // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                delDialog.XamlRoot = XamlRoot;

            var result = await delDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var indexPreset = AppSettings.Preset > -1 ? AppSettings.Preset : 0;

                await LogHelper.Log(
                    $"Showing delete preset dialog: deleting preset \"{PresetManager.Presets[indexPreset].PresetName}\"");


                _presetChanging = true;

                var presetList = new List<Preset>(PresetManager.Presets);
                presetList.RemoveAt(indexPreset);
                PresetManager.Presets = [.. presetList];

                _presetChanging = false;

                AppSettings.Preset = PresetManager.Presets.Length > 0 ? 0 : -1;
                _presetIndex = AppSettings.Preset;

                NotificationsService.ShowNotification("DeleteSuccessTitle".GetLocalized(),
                    "DeleteSuccessDesc".GetLocalized(),
                    InfoBarSeverity.Success);

                PresetManager.SaveSettings();
                LoadPresets();
            }
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);
        }
    }

    private async void EditPresetButton_Click_1(object sender, RoutedEventArgs e)
    {
        try
        {
            var presetName = PresetManager.Presets[_presetIndex].PresetName;
            var presetDesc = PresetManager.Presets[_presetIndex].PresetDesc;
            if (presetName.Contains("Preset_")) presetName = ГлавнаяPage.TryLocalize(presetName);

            if (presetDesc.Contains("Preset_")) presetDesc = ГлавнаяPage.TryLocalize(presetDesc);

            await OpenEditPresetDialog_Click(presetName, presetDesc,
                PresetManager.Presets[_presetIndex].PresetIcon);
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    private async Task OpenEditPresetDialog_Click(string presetName, string presetDesc, string presetIcon)
    {
        // Создаём элементы интерфейса
        var glyph = new FontIcon { Glyph = presetIcon };
        var iconButton = new Button
        {
            Height = 60,
            Width = 60,
            CornerRadius = new CornerRadius(16),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 14),
            Content = glyph
        };

        var nameBox = new TextBox
        {
            Text = presetName,
            MaxLength = 31,
            PlaceholderText = "Param_Preset_New_Name_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 280,
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var descBox = new TextBox
        {
            Text = presetDesc,
            PlaceholderText = "Param_Preset_New_Desc_Add/PlaceholderText".GetLocalized(),
            CornerRadius = new CornerRadius(9),
            Width = 280,
            Margin = new Thickness(0, 6, 0, 0),
            Shadow = (ThemeShadow)Resources["SharedShadow"],
            Translation = new Vector3(0, 0, 10)
        };

        var fieldsPanel = new StackPanel();
        fieldsPanel.Children.Add(nameBox);
        fieldsPanel.Children.Add(descBox);

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 15
        };
        content.Children.Add(iconButton);
        content.Children.Add(fieldsPanel);

        var dialog = new ContentDialog
        {
            Title = "Param_Preset_Edit_Name/Content".GetLocalized(),
            XamlRoot = XamlRoot,
            PrimaryButtonText = "Param_Preset_Edit_Name/Content".GetLocalized(),
            SecondaryButtonText = "Param_Preset_Delete_Preset/Content".GetLocalized(),
            CloseButtonText = "CancelThis/Text".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            Content = content
        };

        // --- Обработчики ---

        // Обработчик выбора иконки — обновляем сразу
        ItemClickEventHandler itemClickHandler = (_, e) =>
        {
            var selectedGlyph = (FontIcon)e.ClickedItem;
            if (selectedGlyph == null) return;

            presetIcon = selectedGlyph.Glyph;
            glyph.Glyph = presetIcon;
        };

        RoutedEventHandler clickHandler = (_, _) =>
        {
            // Подписываемся на выбор иконки
            SelectionGrid.ItemClick -= itemClickHandler; // Избегаем дублирования
            SelectionGrid.ItemClick += itemClickHandler;

            SymbolFlyout.ShowAt(iconButton);
        };

        iconButton.Click += clickHandler;

        // Показываем диалог
        var result = await dialog.ShowAsync();

        // --- Очистка после диалога ---
        iconButton.Click -= clickHandler;
        SelectionGrid.ItemClick -= itemClickHandler;

        // --- Логика результата ---
        if (result == ContentDialogResult.Primary)
            EditPresetButton_Click(nameBox.Text, descBox.Text, presetIcon);
        else if (result == ContentDialogResult.Secondary) DeletePresetButton_Click();
    }

    #endregion

    #endregion

    #endregion

    #region Preset Settings Events

    private void PresetsControl_SelectionChanged(object sender, SelectionChangedEventArgs? e)
    {
        try
        {
            var selectedItem = (sender as PresetSelector)?.SelectedItem;

            if (selectedItem == null) return;

            SelectedPresetName.Text = selectedItem.Text;

            // Обход отсутствия описания, при помощи записывания имени пресета в описание. Чтобы не отображать два раза одну и ту же строку, описание пресета скрывается (так как его нет)
            SelectedPresetDescription.Text = selectedItem.Description != selectedItem.Text
                ? selectedItem.Description
                : string.Empty;

            if (e != null)
            {
                if (_doubleClickApplyToken == SelectedPresetName.Text + SelectedPresetDescription.Text +
                    PresetsControl.SelectedIndex)
                    ApplyButton_Click(null, null);

                _doubleClickApplyToken = SelectedPresetName.Text + SelectedPresetDescription.Text +
                                         PresetsControl.SelectedIndex;
            }

            if (selectedItem.Description == selectedItem.Text)
            {
                SelectedPresetDescription.Visibility = Visibility.Collapsed;
                EditCurrentButtonsStackPanel.Margin = new Thickness(0, 0, -13, -10);
                EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Top;
                SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Center;
            }
            else
            {
                SelectedPresetDescription.Visibility = Visibility.Visible;
                EditCurrentButtonsStackPanel.Margin = new Thickness(0, 17, -13, -10);
                EditCurrentButtonsStackPanel.VerticalAlignment = VerticalAlignment.Center;
                SelectedPresetTextsStackPanel.VerticalAlignment = VerticalAlignment.Top;
            }


            PresetSettingsBeginnerView.Margin = new Thickness(0, 0, 0, 0);

            EditPresetButton.Visibility = Visibility.Visible;

            var selectedIndex = PresetsControl.SelectedIndex;
            if (selectedIndex > -1 && selectedIndex < PresetManager.Presets.Length &&
                selectedItem.Text == PresetManager.Presets[selectedIndex].PresetName)
                AppSettings.Preset = selectedIndex;
            else
                for (var presetIndex = 0; presetIndex < PresetManager.Presets.Length; presetIndex++)
                {
                    var preset = PresetManager.Presets[presetIndex];
                    var presetName = preset.PresetName;
                    var presetDesc = preset.PresetDesc;
                    if (presetName.Contains("Preset_")) presetName = ГлавнаяPage.TryLocalize(presetName);

                    if (presetDesc.Contains("Preset_")) presetDesc = ГлавнаяPage.TryLocalize(presetDesc);

                    if (presetName == selectedItem.Text &&
                        (presetDesc == selectedItem.Description || presetName == selectedItem.Description) &&
                        (preset.PresetIcon == selectedItem.IconGlyph ||
                         preset.PresetIcon == "\uE718"))
                    {
                        AppSettings.Preset = presetIndex;
                        break;
                    }
                }

            _presetIndex = AppSettings.Preset;
            AppSettings.SaveSettings();
            InitializeCustomPresetSettings(_presetIndex);
        }
        catch (Exception ex)
        {
            LogHelper.LogWarn(ex);
        }
    }

    private void AutoTdpSetOnly(ToggleButton toggleButton)
    {
        AutoTdpDisabled.IsChecked = false;
        AutoTdpEnabled.IsChecked = false;
        AutoTdpManual.IsChecked = false;
        
        toggleButton.IsChecked = true;
        
        var normalFontWeight = new FontWeight(400);
        AutoTdpText1.FontSize = 13;
        AutoTdpText1.FontWeight = normalFontWeight;
        AutoTdpText2.FontSize = 13;
        AutoTdpText2.FontWeight = normalFontWeight;
        AutoTdpText3.FontSize = 13;
        AutoTdpText3.FontWeight = normalFontWeight;
        PowerAdjustment.Visibility = toggleButton.Name == "AutoTdpManual" ? Visibility.Visible : Visibility.Collapsed;
        
        var text = VisualTreeHelper.FindVisualChildren<TextBlock>(toggleButton).FirstOrDefault();
        if (text != null)
        {
            text.FontSize = 14;
            text.FontWeight = new  FontWeight(500);
        }
    }
    
    private void AutoTdpEnabled_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement toggle)
        {
            AutoTdpSetOnly((toggle as ToggleButton)!);
            ChangedBaseTdp_Value();
        }
    }
    
    private void TargetNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        var name = sender.Tag.ToString();
            
        if (name != null)
        {
            object sliderObject;

            try
            {
                sliderObject = FindName(name);
            }
            catch (Exception ex)
            {
                LogHelper.TraceIt_TraceError(ex);
                return;
            }

            if (sliderObject is Slider slider)
            {
                if (slider.Maximum < sender.Value)
                {
                    slider.Maximum = FromValueToUpperFive(sender.Value);
                }
            }
        }        
    }

    private static int FromValueToUpperFive(double value) => (int)Math.Ceiling(value / 5) * 5;

    private async void ApplyButton_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            var selectedItem = PresetsControl.SelectedItem;
            var selectedIndex = PresetsControl.SelectedIndex;

            var name = selectedItem.Text;
            var desc = selectedItem.Description;
            var icon = selectedItem.IconGlyph;
            Preset? requiredPreset = null;
            if (selectedIndex > -1 && selectedIndex < PresetManager.Presets.Length &&
                selectedItem.Text == PresetManager.Presets[selectedIndex].PresetName)
            {
                requiredPreset = PresetManager.Presets[selectedIndex];
                AppSettings.Preset = selectedIndex;
            }
            else
            {
                for (var presetIndex = 0; presetIndex < PresetManager.Presets.Length; presetIndex++)
                {
                    var preset = PresetManager.Presets[presetIndex];
                    var presetName = preset.PresetName;
                    var presetDesc = preset.PresetDesc;
                    if (presetName.Contains("Preset_")) presetName = ГлавнаяPage.TryLocalize(presetName);

                    if (presetDesc.Contains("Preset_")) presetDesc = ГлавнаяPage.TryLocalize(presetDesc);

                    if (presetName == name &&
                        (presetDesc == desc || presetName == desc) &&
                        (preset.PresetIcon == icon ||
                         preset.PresetIcon == "\uE718"))
                    {
                        AppSettings.Preset = presetIndex;
                        requiredPreset = preset;
                        break;
                    }
                }
            }

            AppSettings.SaveSettings();

            ПараметрыPage.ApplyInfo = string.Empty;
            if (requiredPreset != null) await Applyer.ApplyPreset(requiredPreset, true);

            await Task.Delay(1000);
            var timer = 1000;
            var applyInfo = ПараметрыPage.ApplyInfo;
            if (applyInfo != string.Empty) timer *= applyInfo.Split('\n').Length + 1;

            DispatcherQueue.TryEnqueue(async void () =>
            {
                try
                {
                    ApplyTeach.Target = ApplyButton;
                    ApplyTeach.Title = "Apply_Success".GetLocalized();
                    ApplyTeach.Subtitle = "";
                    ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                    ApplyTeach.IsOpen = true;
                    var infoSet = InfoBarSeverity.Success;
                    if (applyInfo != string.Empty)
                    {
                        await LogHelper.Log(applyInfo);
                        ApplyTeach.Title = "Apply_Warn".GetLocalized();
                        ApplyTeach.Subtitle = "Apply_Warn_Desc".GetLocalized() + applyInfo;
                        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                        await Task.Delay(timer);
                        ApplyTeach.IsOpen = false;
                        infoSet = InfoBarSeverity.Warning;
                    }
                    else
                    {
                        await Task.Delay(3000);
                        ApplyTeach.IsOpen = false;
                    }

                    NotificationsService.ShowNotification(ApplyTeach.Title,
                        ApplyTeach.Subtitle + (applyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                        infoSet,
                        true);
                }
                catch (Exception ex)
                {
                    await LogHelper.LogError(ex);
                }
            });
        }
        catch (Exception ex)
        {
            await LogHelper.LogWarn(ex);
        }
    }

    /// <summary>
    ///     Изменяет состояние привязанных ToggleSwitch
    /// </summary>
    private void ToggleTheSwitchByTag(object sender, object e)
    {
        if (sender is FrameworkElement { Tag: string targetName })
        {
            // Ищем элемент по имени на текущей странице и меняем его состояние
            var targetToggle = FindName(targetName) as ToggleSwitch;
            if (targetToggle != null) targetToggle.IsOn = !targetToggle.IsOn;
        }
    }

    private void CurveOptimizerLevelCustom_Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_presetChanging) return;


        var index = _presetIndex == -1 ? 0 : _presetIndex;

        PresetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.Value =
            CurveOptimizerLevelCustomSlider.Value;
        PresetManager.SaveSettings();
    }

    private void CurveOptimizerCustom_Toggled(object sender, RoutedEventArgs e)
    {
        if (_presetChanging) return;


        var index = _presetIndex == -1 ? 0 : _presetIndex;

        PresetManager.Presets[index].CurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled =
            CurveOptimizerCustom.IsOn;
        PresetManager.SaveSettings();
    }

    private void CurveOptimizerCustom_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        CurveOptimizerCustom.IsOn = !CurveOptimizerCustom.IsOn;
    }

    #endregion
}