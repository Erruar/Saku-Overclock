using System.Numerics;
using Windows.Foundation.Metadata;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using Saku_Overclock.Services;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using static Saku_Overclock.Styles.BandCrowdToggle;
using Task = System.Threading.Tasks.Task;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IApplyerService Applyer = App.GetService<IApplyerService>();
    private static readonly IOcFinderService OcFinder = App.GetService<IOcFinderService>();
    private static readonly IPresetManagerService PresetManager = App.GetService<IPresetManagerService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool NotReady => !_isLoaded || _presetChanging || AppSettings.Preset < 0;

    private bool
        _presetChanging = true; // Ожидание окончательной смены пресета на другой. Активируется при смене пресета 

    private int _presetIndex; // Выбранный пресет
    private readonly IBackgroundDataUpdater _dataUpdater = App.GetService<IBackgroundDataUpdater>();

    private static readonly IAppNotificationService
        NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения

    private static readonly ICpuService Cpu = App.GetService<ICpuService>();
    private string _doubleClickApplyToken = string.Empty;

    public ПресетыPage()
    {
        InitializeComponent();

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

        LoadPresets();

        var coAvailable = OcFinder.IsUndervoltingAvailable();

        if (coAvailable || true)
        {
            CurveOptimizerCustomGrid.Visibility = Visibility.Visible;
        }
        else
        {
            UndervoltingToggle.State = BandCrowdStates.Off;
            CurveOptimizerCustomGrid.Visibility = Visibility.Collapsed;
        }

        SetCallbacks();
        
        _isLoaded = true;
    }

    private PresetCpuSettings CurrentCpuSettings => PresetManager.Presets[_presetIndex].CpuSettings;
    private PresetVrmSettings CurrentVrmSettings => PresetManager.Presets[_presetIndex].VrmSettings;
    private PresetFrequenciesSettings CurrentFrequenciesSettings => PresetManager.Presets[_presetIndex].FrequenciesSettings;
    private PresetSubsystemsSettings CurrentSubsystemsSettings => PresetManager.Presets[_presetIndex].SubsystemsSettings;
    private PresetCurveOptimizerOptions CurrentCurveOptimizerOptions => PresetManager.Presets[_presetIndex].CurveOptimizerOptions;
    private PresetAdvancedCpuModesSettings CurrentCpuModesSettings => PresetManager.Presets[_presetIndex].CpuModesSettings;
    private void SetCallbacks()
    {
        CpuTemp.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.CpuMaximumTemperature = val);
        GpuTemp.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.IntegratedGpuMaximumTemperature = val);
        CpuPowerLimit.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.CpuSustainedPowerLimit = val);
        CpuActualPower.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.CpuActualPowerLimit = val);
        CpuAveragePower.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.CpuAveragePowerLimit = val);
        GpuPowerLimit.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.IntegratedGpuPowerLimit = val); 
        CpuTurboSlowTime.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.CpuBoostTimeSlow = val);
        CpuTurboFastTime.ValueChanged += val => UpdatePresetSetting(() => CurrentCpuSettings.CpuBoostTimeFast = val);
        CpuFreqRestoreTime.ValueChanged += val => UpdatePresetSetting(() => CurrentVrmSettings.VrmCpuFrequencyRestoreTime = val);
        FixedCpuFrequency.ValueChanged += val => UpdatePresetSetting(() => CurrentFrequenciesSettings.CpuFrequency = val);
        if (IsRavenFamily())
        {
            FixedGpuFrequency.ValueChanged += val => UpdatePresetSetting(() => CurrentSubsystemsSettings.MinimumIntegratedGraphicsFrequency = val);
        }
        else
        {
            FixedGpuFrequency.ValueChanged += val => UpdatePresetSetting(() => CurrentFrequenciesSettings.IntegratedGraphicsFrequency = val);
        }
        
        UndervoltingCpu.ValueChanged += val => UpdatePresetSetting(() => CurrentCurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel = val);
        UndervoltingGpu.ValueChanged += val => UpdatePresetSetting(() => CurrentCurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel = val);
    }
    
    private void UpdatePresetSetting(Action assignmentAction)
    {
        if (NotReady) return;

        assignmentAction();
    
        PresetManager.SaveSettings();
    }

    #region JSON and Initialization

    #region Initialization

    private void LoadPresets()
    {
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

        InitializeCustomPresetSettings(AppSettings.Preset);
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
            var preset = PresetManager.Presets[index];
            var cpu = preset.CpuSettings;
            var vrm = preset.VrmSettings;
            var sub = preset.SubsystemsSettings;
            var co = preset.CurveOptimizerOptions;
            
            // Температуры
            AdaptiveTemperature.State = preset switch
            {
                _ when cpu.AutomaticTemperatureManagement.IsEnabled => BandCrowdStates.Auto,
                _ when cpu.CpuMaximumTemperature.IsEnabled || 
                       cpu.IntegratedGpuMaximumTemperature.IsEnabled => BandCrowdStates.Manual,
                _ => BandCrowdStates.Off
            };
            CpuTemp.Value = cpu.CpuMaximumTemperature;
            GpuTemp.Value = cpu.IntegratedGpuMaximumTemperature;

            // Лимиты TDP
            CpuPowerLimit.Value = cpu.CpuSustainedPowerLimit;
            CpuActualPower.Value = cpu.CpuActualPowerLimit;
            CpuAveragePower.Value = cpu.CpuAveragePowerLimit;
            GpuPowerLimit.Value = cpu.IntegratedGpuPowerLimit;

            AutoTdp.State = preset switch
            {
                _ when cpu.AutomaticPowerManagement.IsEnabled => BandCrowdStates.Auto,
                _ when cpu.CpuSustainedPowerLimit.IsEnabled || 
                       cpu.CpuActualPowerLimit.IsEnabled || 
                       cpu.CpuAveragePowerLimit.IsEnabled => BandCrowdStates.Manual,
                _ => BandCrowdStates.Off
            };
            
            // Управление Turbo Boost
            BetterTurbo.State = preset switch
            {
                _ when cpu.AutomaticTurboManagement.IsEnabled => BandCrowdStates.Auto,
                _ when cpu.CpuBoostTimeSlow.IsEnabled || 
                       cpu.CpuBoostTimeFast.IsEnabled => BandCrowdStates.Manual,
                _ => BandCrowdStates.Off
            };

            CpuTurboSlowTime.Value = cpu.CpuBoostTimeSlow;
            CpuTurboFastTime.Value = cpu.CpuBoostTimeFast;
            CpuFreqRestoreTime.Value = vrm.VrmCpuFrequencyRestoreTime;
            
            // Fix 0.4 GHz 
            CpuFrequency04Fix.State = CurrentCpuModesSettings.CpuFrequency04Fix.IsEnabled
                ? BandCrowdStates.Manual
                : BandCrowdStates.Off;
            
            // Частота процессора
            FixedCpuFrequency.Value = preset.FrequenciesSettings.CpuFrequency;
            
            FixedCpuFrequencyToggle.State = preset.FrequenciesSettings.CpuFrequency.IsEnabled
                ? BandCrowdStates.Manual
                : BandCrowdStates.Off;

            // Частота встроенной графики 
            FixedGpuFrequency.Value = IsRavenFamily() ? sub.MinimumIntegratedGraphicsFrequency 
                : preset.FrequenciesSettings.IntegratedGraphicsFrequency;
            
            FixedIntegratedGpuFrequency.State = sub.MaximumIntegratedGraphicsFrequency.IsEnabled ||
                                                sub.MinimumIntegratedGraphicsFrequency.IsEnabled ||
                                                preset.FrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled
                ? BandCrowdStates.Manual
                : BandCrowdStates.Off;

            // Андервольтинг
            UndervoltingToggle.State = co switch
            {
                _ when co.AutomaticCurveOptimizerManagement.IsEnabled => BandCrowdStates.Auto,
                _ when co.CpuCurveOptimizerUndervoltingLevel.IsEnabled => BandCrowdStates.Manual,
                _ => BandCrowdStates.Off
            };

            if (UndervoltingToggle.State == BandCrowdStates.Manual)
            {
                UndervoltingCpu.Value = co.CpuCurveOptimizerUndervoltingLevel;
                UndervoltingGpu.Value = co.IntegratedGpuCurveOptimizerUndervoltingLevel;
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

    #region Function Helpers

    private static bool IsRavenFamily() => Cpu.GetCodenameGeneration() == CpuService.CodenameGeneration.Fp5;

    private void TryAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    #endregion

    #region Preset Management Dialogs

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

    private void AdaptiveTemperature_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        CurrentCpuSettings.AutomaticTemperatureManagement.IsEnabled = false;
        switch (state)
        {
            case BandCrowdStates.Off:
                CurrentCpuSettings.CpuMaximumTemperature.IsEnabled = false;
                CurrentCpuSettings.IntegratedGpuMaximumTemperature.IsEnabled = false;
                break;
            case BandCrowdStates.Auto:
                CurrentCpuSettings.AutomaticTemperatureManagement.IsEnabled = true;
                CurrentCpuSettings.CpuMaximumTemperature.IsEnabled = false; // will be ignored and auto calculated in Oc Finder
                break;
            case BandCrowdStates.Manual:
                CurrentCpuSettings.CpuMaximumTemperature.IsEnabled = true;
                CpuTemp.Value = null;
                CpuTemp.Value = CurrentCpuSettings.CpuMaximumTemperature;
                break;
        }
        
        PresetManager.SaveSettings();
    }
    
    private void AutoTdp_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        CurrentCpuSettings.AutomaticPowerManagement.IsEnabled = false;
        switch (state)
        {
            case BandCrowdStates.Off:
                CurrentCpuSettings.CpuActualPowerLimit.IsEnabled = false;
                CurrentCpuSettings.CpuAveragePowerLimit.IsEnabled = false;
                CurrentCpuSettings.CpuSustainedPowerLimit.IsEnabled = false;
                CurrentCpuSettings.IntegratedGpuPowerLimit.IsEnabled = false;
                break;
            case BandCrowdStates.Auto:
                CurrentCpuSettings.AutomaticPowerManagement.IsEnabled = true;
                break;
            case BandCrowdStates.Manual:
                CurrentCpuSettings.CpuSustainedPowerLimit.IsEnabled = true;
                CpuPowerLimit.Value = null;
                CpuPowerLimit.Value = CurrentCpuSettings.CpuSustainedPowerLimit;
                break;
        }
        
        PresetManager.SaveSettings();
    }
    
    private void BetterTurbo_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        CurrentCpuSettings.AutomaticTurboManagement.IsEnabled = false;
        switch (state)
        {
            case BandCrowdStates.Off:
                CurrentCpuSettings.CpuBoostTimeSlow.IsEnabled = false;
                CurrentCpuSettings.CpuBoostTimeFast.IsEnabled = false;
                CurrentVrmSettings.VrmCpuFrequencyRestoreTime.IsEnabled = false;
                break;
            case BandCrowdStates.Auto:
                CurrentCpuSettings.AutomaticTurboManagement.IsEnabled = true;
                break;
            case BandCrowdStates.Manual:
                CurrentCpuSettings.CpuBoostTimeSlow.IsEnabled = true;
                CpuTurboSlowTime.Value = null;
                CpuTurboSlowTime.Value = CurrentCpuSettings.CpuBoostTimeSlow;
                break;
        }
        
        PresetManager.SaveSettings();
    }

    private void FixedCpuFrequencyToggle_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        switch (state)
        {
            case BandCrowdStates.Off:
                CurrentFrequenciesSettings.CpuFrequency.IsEnabled = false;
                break;
            case BandCrowdStates.Manual:
                CurrentFrequenciesSettings.CpuFrequency.IsEnabled = true;
                FixedCpuFrequency.Value = null;
                FixedCpuFrequency.Value = CurrentFrequenciesSettings.CpuFrequency;
                break;
        }

        PresetManager.SaveSettings();
    }
    
    private void FixedIntegratedGpuFrequency_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        switch (state)
        {
            case BandCrowdStates.Off:
                if (IsRavenFamily())
                {
                    CurrentSubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = false;
                }
                else
                {
                    CurrentFrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = false;
                }

                break;
            case BandCrowdStates.Manual:
                if (IsRavenFamily())
                {
                    CurrentSubsystemsSettings.MinimumIntegratedGraphicsFrequency.IsEnabled = true;
                    FixedGpuFrequency.Value = null;
                    FixedGpuFrequency.Value = CurrentSubsystemsSettings.MinimumIntegratedGraphicsFrequency;
                }
                else
                {
                    CurrentFrequenciesSettings.IntegratedGraphicsFrequency.IsEnabled = true;
                    FixedGpuFrequency.Value = null;
                    FixedGpuFrequency.Value = CurrentFrequenciesSettings.IntegratedGraphicsFrequency;
                }

                break;
        }

        PresetManager.SaveSettings();
    }

    private void UndervoltingToggle_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        CurrentCurveOptimizerOptions.AutomaticCurveOptimizerManagement.IsEnabled = false;
        switch (state)
        {
            case BandCrowdStates.Off:
                CurrentCurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled = false;
                CurrentCurveOptimizerOptions.IntegratedGpuCurveOptimizerUndervoltingLevel.IsEnabled = false;
                break;
            case BandCrowdStates.Auto:
                CurrentCurveOptimizerOptions.AutomaticCurveOptimizerManagement.IsEnabled = true;
                break;
            case BandCrowdStates.Manual:
                CurrentCurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel.IsEnabled = true;
                UndervoltingCpu.Value = null;
                UndervoltingCpu.Value = CurrentCurveOptimizerOptions.CpuCurveOptimizerUndervoltingLevel;
                break;
        }
        
        PresetManager.SaveSettings();
    }

    #endregion

    private void CpuFrequency04Fix_OnClick(BandCrowdStates state)
    {
        if (NotReady) return;
        
        CurrentCpuModesSettings.CpuFrequency04Fix.IsEnabled = state == BandCrowdStates.Manual;
        PresetManager.SaveSettings();
    }
}