using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля 
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private int _indexprofile = 0; // Выбранный профиль
    private PeriodicTimer? _flipTimer;
    private readonly Random _random = new();
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения


    private List<string> Tips
    {
        get;
    } =
    [
        "Настроить пресеты можно не только в расширенном режиме, но и в обычном",
        "Вы можете переключаться между своими профилями при помощи горячих клавиш",
        "Разгон лучше настраивать поэтапно, а не сразу на максимум",
        "Перед началом разгона убедитесь, что система охлаждения работает корректно, чтобы избежать перегрева компонентов",
        "Увеличивайте частоту постепенно и проводите тесты стабильности после каждого шага, чтобы система оставалась надежной",
        "Сохраняйте профили для разных режимов работы, вы сможете быстро переключаться между скоростью и энергосбережением",
        "Регулярно отслеживайте температуру процессора и видеокарты – стабильный разгон невозможен без контроля за температурой",
        "Убедитесь, что у вас установлены последние версии драйверов и прошивок – это может значительно улучшить производительность",
        "Проводите стресс-тесты после изменений – только стабильная конфигурация гарантирует долгую и безопасную работу системы",
        "Перед экспериментами сохраняйте свои данные – это поможет быстро вернуться к своей работе в случае сбоя",
        "При использовании андервольтинга не снижайте напряжение слишком сильно – это приведёт к моментальному зависанию процессора",
        "Используйте горячие клавиши для быстрого переключения между профилями – это удобно при изменении нагрузки на процессор",
        "Включите автостарт Saku Overclock при запуске системы, если вам нужно постоянное применение настроек разгона",
        "Если ноутбук работает от батареи, используйте профили с низким энергопотреблением, чтобы увеличить время автономной работы",
        "Не изменяйте настройки P-States в BIOS, если не уверены – это может привести к нестабильной работе или даже выходу из строя процессора",
        "Для процессоров важны не только частоты, но и температурные лимиты – зачастую их лучше поднять",
        "Разгон процессора без настройки работы кулера может привести к перегреву – настройте охлаждение в BIOS или Saku Overclock",
        "Если при изменении настроек система зависает, попробуйте перезагрузиться, программа автоматически выключит настройки со сбоями",
        "Почаще смотрите на страницу информации, если хотите проанализировать поведение процессора и выявить причины нестабильности",
        "Некоторые ноутбуки имеют ограничения со стороны BIOS, знайте что ничего не поделать, если разгон не работает",
        "Используйте тесты, такие как Cinebench или OCCT, для проверки стабильности разгона, но не забывайте о реальных нагрузках",
        "Не гонитесь за максимальной частотой – лучше найти баланс между производительностью, стабильностью и нагревом",
        "Если ваш процессор сбрасывает частоты после разгона, проверьте лимиты по питанию, возможно их стоит увеличить"
    ];

    public ПресетыPage()
    {
        InitializeComponent();
        AppSettings.NBFCFlagConsoleCheckSpeedRunning = false;
        AppSettings.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        AppSettings.SaveSettings();
        Loaded += ПресетыPage_Loaded;
    }

    private async void ПресетыPage_Loaded(object sender, RoutedEventArgs e)
    {
        _waitforload = false;
        SelectedProfile_Description.Text = "Preset_Min_Desc/Text".GetLocalized();
        TipsFlipView.ItemsSource = Tips;
        LoadProfiles();

        // Загрузить остальные UI элементы, функции блока "Дополнительно"
        O1.IsOn = AppSettings.CurveOptimizerOverallEnabled;
        O1m.SelectedIndex = AppSettings.CurveOptimizerOverallLevel;
        RtssOverlaySwitch.IsOn = AppSettings.RTSSMetricsEnabled;
        TrayMonFeatSwitch.IsOn = AppSettings.NiIconsEnabled;
        StreamOptimizerSwitch.IsOn = AppSettings.StreamOptimizerEnabled;
        if (AppSettings.ProfilespageViewModeBeginner)
        {
            ToolTipService.SetToolTip(CurrentMode_Button, "Param_ProMode".GetLocalized());
            CurrentMode_Icon.Glyph = "\uE9F5";
        }
        else
        {
            ToolTipService.SetToolTip(CurrentMode_Button, "Param_NewbieMode".GetLocalized());
            CurrentMode_Icon.Glyph = "\uF4A5";
        }

        _isLoaded = true;

        // Таймер для смены страниц раз в 10 секунд
        _flipTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await _flipTimer.WaitForNextTickAsync())
        {
            SwitchRandomPage();
        }

    }

    #region JSON and Initialization

    #region Initialization
    private async void LoadProfiles()
    {
        // Загрузить профили перед началом работы с ними
        ProfileLoad();

        // Очистить элементы ProfilesControl
        ProfilesControl.Items.Clear();

        // Пройтись по каждому профилю и добавить их в ProfilesControl
        foreach (var profile in _profile)
        {
            var isChecked = AppSettings.Preset != -1 &&
                            _profile[AppSettings.Preset].profilename == profile.profilename &&
                            _profile[AppSettings.Preset].profiledesc == profile.profiledesc &&
                            _profile[AppSettings.Preset].profileicon == profile.profileicon;


            var toggleButton = new ProfileItem
            {
                IsSelected = isChecked,
                IconGlyph = profile.profileicon == string.Empty ? "\uE718" : profile.profileicon,
                Text = profile.profilename,
                Description = profile.profiledesc
            };
            ProfilesControl.Items.Add(toggleButton);
        }


        // Готовые Пресеты
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uEcad",
            Text = "Preset_Max_Name/Text".GetLocalized(), // Maximum
            Description = "Preset_Max_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\ue945",
            Text = "Preset_Speed_Name/Text".GetLocalized(), // Speed
            Description = "Preset_Speed_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uec49",
            Text = "Preset_Balance_Name/Text".GetLocalized(), // Balance
            Description = "Preset_Balance_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uec0a",
            Text = "Preset_Eco_Name/Text".GetLocalized(), // Eco
            Description = "Preset_Eco_Desc/Text".GetLocalized()
        });
        ProfilesControl.Items.Add(new ProfileItem
        {
            IsSelected = AppSettings.Preset == -1 && AppSettings.PremadeMaxActivated,
            IconGlyph = "\uebc0",
            Text = "Preset_Min_Name/Text".GetLocalized(), // Minimum
            Description = "Preset_Min_Desc/Text".GetLocalized()
        });

        // Workaround чтобы все элементы корректно загрузились в ProfilesControl
        ProfilesControl.UpdateView();

        foreach (var item in ProfilesControl.Items)
        {
            if (item.IsSelected)
            {
                SelectedProfile_Name.Text = item.Text;
                SelectedProfile_Description.Text = item.Description;
            }
        }
        if (AppSettings.Preset != -1)
        {
            await MainInitAsync(AppSettings.Preset);
        }
    }

    private async Task MainInitAsync(int index)
    {
        _waitforload = true;

        ProfileLoad();
        try
        {
            if (_profile[index].cpu1value > c1v.Maximum)
            {
                c1v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu1value);
            }

            if (_profile[index].cpu2value > c2v.Maximum)
            {
                c2v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu2value);
            }

            if (_profile[index].cpu3value > c3v.Maximum)
            {
                c3v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu3value);
            }

            if (_profile[index].cpu4value > c4v.Maximum)
            {
                c4v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu4value);
            }

            if (_profile[index].cpu5value > c5v.Maximum)
            {
                c5v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu5value);
            }

            if (_profile[index].cpu6value > c6v.Maximum)
            {
                c6v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].cpu6value);
            }

            if (_profile[index].vrm1value > V1V.Maximum)
            {
                V1V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm1value);
            }

            if (_profile[index].vrm2value > V2V.Maximum)
            {
                V2V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm2value);
            }

            if (_profile[index].vrm3value > V3V.Maximum)
            {
                V3V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm3value);
            }

            if (_profile[index].vrm4value > V4V.Maximum)
            {
                V4V.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].vrm4value);
            }

            if (_profile[index].gpu9value > g9v.Maximum)
            {
                g9v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].gpu9value);
            }

            if (_profile[index].gpu10value > g10v.Maximum)
            {
                g10v.Maximum = ПараметрыPage.FromValueToUpperFive(_profile[index].gpu10value);
            }
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }

        try
        {
            c1.IsChecked = _profile[index].cpu1;
            c1v.Value = _profile[index].cpu1value;
            c2.IsChecked = _profile[index].cpu2;
            c2v.Value = _profile[index].cpu2value;
            c3.IsChecked = _profile[index].cpu3;
            c3v.Value = _profile[index].cpu3value;
            c4.IsChecked = _profile[index].cpu4;
            c4v.Value = _profile[index].cpu4value;
            c5.IsChecked = _profile[index].cpu5;
            c5v.Value = _profile[index].cpu5value;
            c6.IsChecked = _profile[index].cpu6;
            c6v.Value = _profile[index].cpu6value;
            V1.IsChecked = _profile[index].vrm1;
            V1V.Value = _profile[index].vrm1value;
            V2.IsChecked = _profile[index].vrm2;
            V2V.Value = _profile[index].vrm2value;
            V3.IsChecked = _profile[index].vrm3;
            V3V.Value = _profile[index].vrm3value;
            V4.IsChecked = _profile[index].vrm4;
            V4V.Value = _profile[index].vrm4value;
            g9v.Value = _profile[index].gpu9value;
            g9.IsChecked = _profile[index].gpu9;
            g10v.Value = _profile[index].gpu10value;
            g10.IsChecked = _profile[index].gpu10;
            O1.IsOn = _profile[index].coall;
            O1m.SelectedIndex = _profile[index].coallvalue < 5 ? 0 :
                (_profile[index].coallvalue < 10 ? 1 :
                (_profile[index].coallvalue < 15 ? 2 : 3));
        }
        catch
        {
            await LogHelper.LogError("Profile contains error. Creating new profile.");

            _profile = new Profile[1];
            _profile[0] = new Profile();
            ProfileSave();
        }

        _waitforload = false;
    }

    #endregion

    #region JSON voids
    private static void ProfileSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),
                "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                JsonConvert.SerializeObject(_profile, Formatting.Indented));
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    private static void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json"))!;
        }
        catch (Exception ex)
        {
            JsonRepair('p');
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }
    }

    private static void JsonRepair(char file)
    {
        switch (file)
        {
            case 'p':
                _profile = [];
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\profile.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }

                break;
        }
    }

    #endregion

    #endregion

    #region Event Handlers

    #region Additional Functions
    private void O1m_Loaded(object sender, RoutedEventArgs e)
    {
        var presenter = Saku_Overclock.Helpers.VisualTreeHelper.FindVisualChildren<ContentPresenter>(O1m);
        foreach (var present in presenter)
        {
            present.Margin = new Thickness(7, 1, -30, 1);
            present.HorizontalAlignment = HorizontalAlignment.Center;
        }
        var anicon = Saku_Overclock.Helpers.VisualTreeHelper.FindVisualChildren<AnimatedIcon>(O1m);
        foreach (var icon in anicon)
        {
            icon.Visibility = Visibility.Collapsed;
        }
        var texts = Saku_Overclock.Helpers.VisualTreeHelper.FindVisualChildren<TextBlock>(O1m);
        foreach (var text in texts)
        {
            text.FontWeight = new Windows.UI.Text.FontWeight(700);
            text.FontSize = 17;
        }
    }
    private void O1m_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var texts = Saku_Overclock.Helpers.VisualTreeHelper.FindVisualChildren<TextBlock>(O1m);
        foreach (var text in texts)
        {
            text.FontWeight = new Windows.UI.Text.FontWeight(700);
            text.FontSize = 16;
        }
    }

    private void O1_Toggled(object sender, RoutedEventArgs e) // НАстройки андервольтинга
    {
        if (!_isLoaded) { return; }
        AppSettings.CurveOptimizerOverallEnabled = O1.IsOn;
        AppSettings.CurveOptimizerOverallLevel = O1m.SelectedIndex;
        AppSettings.SaveSettings();
    }

    private void O1m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.CurveOptimizerOverallEnabled = O1.IsOn;
        AppSettings.CurveOptimizerOverallLevel = O1m.SelectedIndex;
        AppSettings.SaveSettings();
    }

    private void RtssOverlaySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.RTSSMetricsEnabled = RtssOverlaySwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void TrayMonFeatSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.NiIconsEnabled = TrayMonFeatSwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void StreamOptimizerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded) { return; }
        AppSettings.StreamOptimizerEnabled = StreamOptimizerSwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void AnimatedToggleButton_Click(object sender, RoutedEventArgs e)
    {
        /*var pciAddress = MakePciAddress(0x0, 0x18, 0x5, 0xE8);
        var regValue = ReadPciReg((uint)pciAddress);*/
        var tdp = GetProcessorTdp(0x00, 0x18, 0x04);
        App.MainWindow.ShowMessageDialogAsync($"TDP процессора: {tdp:F2} Вт", "Result:");
        AppSettings.ProfilespageViewModeBeginner = !AppSettings.ProfilespageViewModeBeginner;
        AppSettings.SaveSettings();
        if (AppSettings.ProfilespageViewModeBeginner)
        {
            ToolTipService.SetToolTip(CurrentMode_Button, "Param_ProMode".GetLocalized());
            CurrentMode_Icon.Glyph = "\uE9F5";
        }
        else
        {
            ToolTipService.SetToolTip(CurrentMode_Button, "Param_NewbieMode".GetLocalized());
            CurrentMode_Icon.Glyph = "\uF4A5";
        }
    }

    // Определения регистров из BKDG
    private const int REG_PROCESSOR_TDP = 0x1B8;
    private const int REG_TDP_LIMIT3 = 0xE8;

    /// <summary>
    /// Заглушка для чтения 32-битного значения из PCI Configuration Space.
    /// Реализуйте этот метод согласно вашей среде (например, через WinRing0).
    /// </summary>
    /// <param name="bus">Номер шины</param>
    /// <param name="device">Номер устройства</param>
    /// <param name="function">Номер функции</param>
    /// <param name="offset">Смещение регистра</param>
    /// <returns>Содержимое регистра (DWORD)</returns>
    public uint ReadPciConfigDword(byte bus, byte device, byte function, int offset)
    {
        // Здесь необходимо реализовать чтение из PCI.
        // Например, через WinRing0 или другой драйвер.
        // Сейчас выбрасываем исключение, чтобы показать, что реализация отсутствует.
        var pciAddress = MakePciAddress(bus, device, function, offset);
        var regValue = ReadPciReg((uint)pciAddress);
        return regValue;
    }

    /// <summary>
    /// Получает TDP процессора в ваттах.
    /// Параметры bus, device и function указывают на устройство, 
    /// где функция (например, 0x04) содержит REG_PROCESSOR_TDP, 
    /// а функция 0x05 — REG_TDP_LIMIT3.
    /// </summary>
    /// <param name="bus">Номер шины (например, 0)</param>
    /// <param name="device">Номер устройства (например, 0x18)</param>
    /// <param name="function">Номер функции для REG_PROCESSOR_TDP (например, 0x04)</param>
    /// <returns>TDP в ваттах</returns>
    public double GetProcessorTdp(byte bus, byte device, byte function)
    {
        // Чтение регистра REG_PROCESSOR_TDP (из функции, например, 0x04)
        var tdpRegValue = ReadPciConfigDword(bus, device, function, REG_PROCESSOR_TDP);

        // Верхние 16 бит — BaseTdp, нижние 16 бит — дополнительное значение
        var baseTdp = tdpRegValue >> 16;
        var tdpRaw = tdpRegValue & 0xFFFF;

        // Чтение регистра REG_TDP_LIMIT3 из функции 0x05 (для того же устройства)
        var tdpLimit3Value = ReadPciConfigDword(bus, device, 5, REG_TDP_LIMIT3);

        // Вычисляем коэффициент перевода: ((val & 0x3FF) << 6) | ((val >> 10) & 0x3F)
        var tdpToWatts = (((tdpLimit3Value & 0x3FF) << 6) | ((tdpLimit3Value >> 10) & 0x3F));

        // Умножаем нижние 16 бит на коэффициент перевода
        var product = (ulong)tdpRaw * tdpToWatts;

        // Пересчёт в микроватты:
        // Исходное значение задано в виде фиксированной точки с масштабом 1/(2^16).
        // Для перевода в микроватты используем: (product * 15625) >> 10.
        var processorPwrMicroWatt = (uint)((product * 15625UL) >> 10);

        // Переводим микроватты в ватты
        var processorPwrWatts = processorPwrMicroWatt / 1_000_000.0;

        return processorPwrWatts;
    }

    long MakePciAddress(int bus, int device, int fn, int offset)
    {
        return 0x80000000 | bus << 16 | device << 11 | fn << 8 | offset;
    }
    uint ReadPciReg(uint pciDev)
    {
        const ushort PCI_ADDR_PORT = 0xCF8;
        const ushort PCI_DATA_PORT = 0xCFC;
        uint pciReg = 0;
        var ols = new Ols(); // Winring0
        ols.WriteIoPortDwordEx(PCI_ADDR_PORT, pciDev);
        ols.ReadIoPortDwordEx(PCI_DATA_PORT, ref pciReg);
        ols.Dispose();
        return pciReg;
    }
    #endregion

    #region Page Helpers Events
    private void SwitchRandomPage()
    {
        if (TipsFlipView.Items.Count > 1)
        {
            var randomIndex = _random.Next(0, Tips.Count);
            TipsFlipView.SelectedIndex = randomIndex;
        }
    }

    private void TryAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    #endregion

    #region OLD Methods
    private void Min_btn_Checked()
    {
        AppSettings.PremadeMinActivated = true;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenADJline =
            " --tctl-temp=60 " + //
            "--stapm-limit=9000 " + //
            "--fast-limit=9000 " + //
            "--stapm-time=900 " + //
            "--slow-limit=6000 " + //
            "--slow-time=900 " + //
            "--vrm-current=120000 " + //
            "--vrmmax-current=120000 " + //
            "--vrmsoc-current=120000 " + //
            "--vrmsocmax-current=120000 " + //
            "--vrmgfx-current=120000 " +
            "--prochot-deassertion-ramp=2 ";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenADJline, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Eco_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = true;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenADJline =
            " --tctl-temp=68 --stapm-limit=15000  --fast-limit=18000 --stapm-time=500 --slow-limit=16000 --slow-time=500 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2 ";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenADJline, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Balance_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = true;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenADJline =
            " --tctl-temp=75 --stapm-limit=17000  --fast-limit=20000 --stapm-time=64 --slow-limit=19000 --slow-time=128 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenADJline, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Speed_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = true;
        AppSettings.PremadeMaxActivated = false;
        AppSettings.RyzenADJline =
            " --tctl-temp=80 --stapm-limit=20000  --fast-limit=20000 --stapm-time=32 --slow-limit=20000 --slow-time=64 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenADJline, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    private void Max_btn_Checked()
    {
        AppSettings.PremadeMinActivated = false;
        AppSettings.PremadeEcoActivated = false;
        AppSettings.PremadeBalanceActivated = false;
        AppSettings.PremadeSpeedActivated = false;
        AppSettings.PremadeMaxActivated = true;
        AppSettings.RyzenADJline =
            " --tctl-temp=90 --stapm-limit=45000  --fast-limit=60000 --stapm-time=80 --slow-limit=60000 --slow-time=1 --vrm-current=120000 --vrmmax-current=120000 --vrmsoc-current=120000 --vrmsocmax-current=120000 --vrmgfx-current=120000 --prochot-deassertion-ramp=2";
        AppSettings.SaveSettings();
        MainWindow.Applyer.Apply(AppSettings.RyzenADJline, false, AppSettings.ReapplyOverclock,
            AppSettings.ReapplyOverclockTimer);
    }

    #endregion

    #endregion

    #region Profile Settings Events

    private async void ProfilesControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //await App.MainWindow.ShowMessageDialogAsync((sender as Saku_Overclock.Styles.ProfileSelector).SelectedItem.Text, "Selected:");
        //(sender as Saku_Overclock.Styles.ProfileSelector).SelectedItem.Text
        var selectedItem = (sender as ProfileSelector)!.SelectedItem;
        if (selectedItem != null)
        {
            SelectedProfile_Name.Text = selectedItem.Text;
            SelectedProfile_Description.Text = selectedItem.Description;
            if ((selectedItem.Text == "Preset_Max_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Max_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Speed_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Balance_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Eco_Desc/Text".GetLocalized()) ||
                (selectedItem.Text == "Preset_Min_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Min_Desc/Text".GetLocalized())
                )
            {
                ProfileSettings_StackPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ProfileSettings_StackPanel.Visibility = Visibility.Visible;
                for (var i = 0; i < _profile.Length; i++)
                {
                    if (_profile[i].profiledesc == selectedItem.Description &&
                        _profile[i].profilename == selectedItem.Text &&
                        _profile[i].profileicon == selectedItem.IconGlyph)
                    {
                        _indexprofile = i;
                        AppSettings.Preset = i;
                        AppSettings.SaveSettings();
                        await MainInitAsync(i);
                        break;
                    }
                }
            }
        }
    }

    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        var endMode = "Balance";
        ProfileItem? selectedItem = null;
        foreach (var item in ProfilesControl.Items)
        {
            if (item.IsSelected)
            {
                selectedItem = item;
            }
        }
        if (selectedItem == null) { return; }
        if (selectedItem.Text == "Preset_Max_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Max_Desc/Text".GetLocalized())
        {
            endMode = "Max";
        }
        else
        if (selectedItem.Text == "Preset_Speed_Name/Text".GetLocalized() &&
                selectedItem.Description == "Preset_Speed_Desc/Text".GetLocalized())
        {
            endMode = "Speed";
        }
        else
        if (selectedItem.Text == "Preset_Balance_Name/Text".GetLocalized() &&
        selectedItem.Description == "Preset_Balance_Desc/Text".GetLocalized())
        {
            endMode = "Balance";
        }
        else
        if (selectedItem.Text == "Preset_Eco_Name/Text".GetLocalized() &&
        selectedItem.Description == "Preset_Eco_Desc/Text".GetLocalized())
        {
            endMode = "Eco";
        }
        else
        if (selectedItem.Text == "Preset_Min_Name/Text".GetLocalized() &&
        selectedItem.Description == "Preset_Min_Desc/Text".GetLocalized())
        {
            endMode = "Min";
        }
        else
        {
            var name = selectedItem.Text;
            var desc = selectedItem.Description;
            var icon = selectedItem.IconGlyph;
            for (var i = 0; i < _profile.Length; i++)
            {
                var profile = _profile[i];
                if (profile.profilename == name &&
                    profile.profiledesc == desc &&
                    (profile.profileicon == icon ||
                     profile.profileicon == "\uE718"))
                {
                    ПараметрыPage.ApplyInfo = string.Empty;
                    ShellPage.MandarinSparseUnitProfile(profile, true);

                    NotificationsService.Notifies ??= [];
                    NotificationsService.Notifies.Add(new Notify
                    {
                        Title = "Profile_APPLIED",
                        Msg = "DEBUG MESSAGE",
                        Type = InfoBarSeverity.Informational
                    });
                    NotificationsService.SaveNotificationsSettings();


                    await Task.Delay(1000);
                    var timer = 1000;
                    if (ПараметрыPage.ApplyInfo != string.Empty)
                    {
                        timer *= ПараметрыPage.ApplyInfo.Split('\n').Length + 1;
                    }
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        ApplyTeach.Target = ApplyButton;
                        ApplyTeach.Title = "Apply_Success".GetLocalized();
                        ApplyTeach.Subtitle = "Apply_Success_Desc".GetLocalized();
                        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
                        ApplyTeach.IsOpen = true;
                        var infoSet = InfoBarSeverity.Success;
                        if (ПараметрыPage.ApplyInfo != string.Empty)
                        {
                            await LogHelper.Log(ПараметрыPage.ApplyInfo);
                            ApplyTeach.Title = "Apply_Warn".GetLocalized();
                            ApplyTeach.Subtitle = "Apply_Warn_Desc".GetLocalized() + ПараметрыPage.ApplyInfo;
                            ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.ReportHacked };
                            await Task.Delay(timer);
                            ApplyTeach.IsOpen = false;
                            infoSet = InfoBarSeverity.Warning;
                        }
                        else
                        {
                            await LogHelper.Log("Apply_Success".GetLocalized());
                            await Task.Delay(3000);
                            ApplyTeach.IsOpen = false;
                        }

                        NotificationsService.Notifies ??= [];
                        NotificationsService.Notifies.Add(new Notify
                        {
                            Title = ApplyTeach.Title,
                            Msg = ApplyTeach.Subtitle + (ПараметрыPage.ApplyInfo != string.Empty ? "DELETEUNAVAILABLE" : ""),
                            Type = infoSet
                        });
                        NotificationsService.SaveNotificationsSettings();
                    });
                    return;
                }
            }
        }

        ShellPage.NextPremadeProfile_Activate(endMode);

        var (_, _, _, settings, _) = ShellPage.PremadedProfiles[endMode];

        AppSettings.RyzenADJline = settings;
        AppSettings.SaveSettings();

        MainWindow.Applyer.ApplyWithoutAdjLine(false);

        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
        {
            Title = "Profile_APPLIED",
            Msg = "DEBUG MESSAGE",
            Type = InfoBarSeverity.Informational
        });
        NotificationsService.SaveNotificationsSettings();

        ApplyTeach.Target = ApplyButton;
        ApplyTeach.Title = "Apply_Success".GetLocalized();
        ApplyTeach.Subtitle = "Apply_Success_Desc".GetLocalized();
        ApplyTeach.IconSource = new SymbolIconSource { Symbol = Symbol.Accept };
        ApplyTeach.IsOpen = true;
        await LogHelper.Log("Apply_Success".GetLocalized());
        await Task.Delay(3000);
        ApplyTeach.IsOpen = false;
    }

    #region Sliders
    //Параметры процессора, при изменении слайдеров
    //Максимальная температура CPU (C)
    private void C1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu1value = c1v.Value;
            ProfileSave();
        }
    }

    //Лимит CPU (W)
    private void C2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu2value = c2v.Value;
            ProfileSave();
        }
    }

    //Реальный CPU (W)
    private void C3_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu3value = c3v.Value;
            ProfileSave();
        }
    }

    //Средний CPU(W)
    private void C4_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu4value = c4v.Value;
            ProfileSave();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu5value = c5v.Value;
            ProfileSave();
        }
    }

    //Тик медленного разгона (S)
    private void C6_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu6value = c6v.Value;
            ProfileSave();
        }
    }

    //Параметры VRM
    private void V1v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm1value = V1V.Value;
            ProfileSave();
        }
    }

    private void V2v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm2value = V2V.Value;
            ProfileSave();
        }
    }

    private void V3v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm3value = V3V.Value;
            ProfileSave();
        }
    }

    private void V4v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm4value = V4V.Value;
            ProfileSave();
        }
    }

    private void G9v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu9value = g9v.Value;
            ProfileSave();
        }
    }

    private void G10v_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu10value = g10v.Value;
            ProfileSave();
        }
    }
    #endregion

    #region CheckBoxes
    //Параметры процессора
    //Максимальная температура CPU (C)
    private void C1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu1 = check;
            _profile[_indexprofile].cpu1value = c1v.Value;
            ProfileSave();
        }
    }

    //Лимит CPU (W)
    private void C2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu2 = check;
            _profile[_indexprofile].cpu2value = c2v.Value;
            ProfileSave();
        }
    }

    //Реальный CPU (W)
    private void C3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu3 = check;
            _profile[_indexprofile].cpu3value = c3v.Value;
            ProfileSave();
        }
    }

    //Средний CPU (W)
    private void C4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu4 = check;
            _profile[_indexprofile].cpu4value = c4v.Value;
            ProfileSave();
        }
    }

    //Тик быстрого разгона (S)
    private void C5_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c5.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu5 = check;
            _profile[_indexprofile].cpu5value = c5v.Value;
            ProfileSave();
        }
    }

    //Тик медленного разгона (S)
    private void C6_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = c6.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].cpu6 = check;
            _profile[_indexprofile].cpu6value = c6v.Value;
            ProfileSave();
        }
    }

    //Параметры VRM
    //Максимальный ток VRM A
    private void V1_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V1.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm1 = check;
            _profile[_indexprofile].vrm1value = V1V.Value;
            ProfileSave();
        }
    }

    //Лимит по току VRM A
    private void V2_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V2.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm2 = check;
            _profile[_indexprofile].vrm2value = V2V.Value;
            ProfileSave();
        }
    }

    //Максимальный ток SOC A
    private void V3_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V3.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm3 = check;
            _profile[_indexprofile].vrm3value = V3V.Value;
            ProfileSave();
        }
    }

    //Лимит по току SOC A
    private void V4_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = V4.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].vrm4 = check;
            _profile[_indexprofile].vrm4value = V4V.Value;
            ProfileSave();
        }
    }

    //Минимальная частота iGpu
    private void G9_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g9.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu9 = check;
            _profile[_indexprofile].gpu9value = g9v.Value;
            ProfileSave();
        }
    }

    //Максимальная частота iGpu
    private void G10_Checked(object sender, RoutedEventArgs e)
    {
        if (_isLoaded == false || _waitforload)
        {
            return;
        }

        ProfileLoad();
        var check = g10.IsChecked == true;
        if (_indexprofile != -1)
        {
            _profile[_indexprofile].gpu10 = check;
            _profile[_indexprofile].gpu10value = g10v.Value;
            ProfileSave();
        }
    }
    #endregion

    #region NumberBoxes
    private void C2t_FocusEngaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden;
        }
    }

    private void C2t_FocusDisengaged(object sender, object args)
    {
        if (sender is NumberBox numberBox)
        {
            numberBox.SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline;
        }
    }

    private void C2t_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        {
            object slider;
            if (sender.Name.Contains('v'))
            {
                slider = FindName(sender.Name.Replace('t', 'V').Replace('v', 'V'));
            }
            else
            {
                try
                {
                    slider = FindName(sender.Name.Replace('t', 'v'));
                }
                catch (Exception ex)
                {
                    SendSmuCommand.TraceIt_TraceError(ex.ToString());
                    return;
                }
            }

            if (slider is Slider slider1)
            {
                if (slider1.Maximum < sender.Value)
                {
                    slider1.Maximum = ПараметрыPage.FromValueToUpperFive(sender.Value);
                }
            }
        }
    }

    #endregion

    #endregion


}
public class Ols : IDisposable
{
    const string dllNameX64 = "WinRing0x64.dll";
    const string dllName = "WinRing0.dll";

    // for this support library
    public enum Status
    {
        NO_ERROR = 0,
        DLL_NOT_FOUND = 1,
        DLL_INCORRECT_VERSION = 2,
        DLL_INITIALIZE_ERROR = 3,
    }

    // for WinRing0
    public enum OlsDllStatus
    {
        OLS_DLL_NO_ERROR = 0,
        OLS_DLL_UNSUPPORTED_PLATFORM = 1,
        OLS_DLL_DRIVER_NOT_LOADED = 2,
        OLS_DLL_DRIVER_NOT_FOUND = 3,
        OLS_DLL_DRIVER_UNLOADED = 4,
        OLS_DLL_DRIVER_NOT_LOADED_ON_NETWORK = 5,
        OLS_DLL_UNKNOWN_ERROR = 9
    }

    // for WinRing0
    public enum OlsDriverType
    {
        OLS_DRIVER_TYPE_UNKNOWN = 0,
        OLS_DRIVER_TYPE_WIN_9X = 1,
        OLS_DRIVER_TYPE_WIN_NT = 2,
        OLS_DRIVER_TYPE_WIN_NT4 = 3,    // Obsolete
        OLS_DRIVER_TYPE_WIN_NT_X64 = 4,
        OLS_DRIVER_TYPE_WIN_NT_IA64 = 5
    }

    // for WinRing0
    public enum OlsErrorPci : uint
    {
        OLS_ERROR_PCI_BUS_NOT_EXIST = 0xE0000001,
        OLS_ERROR_PCI_NO_DEVICE = 0xE0000002,
        OLS_ERROR_PCI_WRITE_CONFIG = 0xE0000003,
        OLS_ERROR_PCI_READ_CONFIG = 0xE0000004
    }

    // Bus Number, Device Number and Function Number to PCI Device Address
    public uint PciBusDevFunc(uint bus, uint dev, uint func)
    {
        return ((bus & 0xFF) << 8) | ((dev & 0x1F) << 3) | (func & 7);
    }

    // PCI Device Address to Bus Number
    public uint PciGetBus(uint address)
    {
        return ((address >> 8) & 0xFF);
    }

    // PCI Device Address to Device Number
    public uint PciGetDev(uint address)
    {
        return ((address >> 3) & 0x1F);
    }

    // PCI Device Address to Function Number
    public uint PciGetFunc(uint address)
    {
        return (address & 7);
    }

    [DllImport("kernel32")]
    public extern static IntPtr LoadLibrary(string lpFileName);


    [DllImport("kernel32", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    private IntPtr module = IntPtr.Zero;
    private uint status = (uint)Status.NO_ERROR;

    public Ols()
    {
        string fileName;
         
            fileName = dllNameX64; 

        module = Ols.LoadLibrary(fileName);
        if (module == IntPtr.Zero)
        {
            status = (uint)Status.DLL_NOT_FOUND;
        }
        else
        {
            GetDllStatus = (_GetDllStatus)GetDelegate("GetDllStatus", typeof(_GetDllStatus));
            GetDllVersion = (_GetDllVersion)GetDelegate("GetDllVersion", typeof(_GetDllVersion));
            GetDriverVersion = (_GetDriverVersion)GetDelegate("GetDriverVersion", typeof(_GetDriverVersion));
            GetDriverType = (_GetDriverType)GetDelegate("GetDriverType", typeof(_GetDriverType));

            InitializeOls = (_InitializeOls)GetDelegate("InitializeOls", typeof(_InitializeOls));
            DeinitializeOls = (_DeinitializeOls)GetDelegate("DeinitializeOls", typeof(_DeinitializeOls));

            IsCpuid = (_IsCpuid)GetDelegate("IsCpuid", typeof(_IsCpuid));
            IsMsr = (_IsMsr)GetDelegate("IsMsr", typeof(_IsMsr));
            IsTsc = (_IsTsc)GetDelegate("IsTsc", typeof(_IsTsc));
            Hlt = (_Hlt)GetDelegate("Hlt", typeof(_Hlt));
            HltTx = (_HltTx)GetDelegate("HltTx", typeof(_HltTx));
            HltPx = (_HltPx)GetDelegate("HltPx", typeof(_HltPx));
            Rdmsr = (_Rdmsr)GetDelegate("Rdmsr", typeof(_Rdmsr));
            RdmsrTx = (_RdmsrTx)GetDelegate("RdmsrTx", typeof(_RdmsrTx));
            RdmsrPx = (_RdmsrPx)GetDelegate("RdmsrPx", typeof(_RdmsrPx));
            Wrmsr = (_Wrmsr)GetDelegate("Wrmsr", typeof(_Wrmsr));
            WrmsrTx = (_WrmsrTx)GetDelegate("WrmsrTx", typeof(_WrmsrTx));
            WrmsrPx = (_WrmsrPx)GetDelegate("WrmsrPx", typeof(_WrmsrPx));
            Rdpmc = (_Rdpmc)GetDelegate("Rdpmc", typeof(_Rdpmc));
            RdpmcTx = (_RdpmcTx)GetDelegate("RdpmcTx", typeof(_RdpmcTx));
            RdpmcPx = (_RdpmcPx)GetDelegate("RdpmcPx", typeof(_RdpmcPx));
            Cpuid = (_Cpuid)GetDelegate("Cpuid", typeof(_Cpuid));
            CpuidTx = (_CpuidTx)GetDelegate("CpuidTx", typeof(_CpuidTx));
            CpuidPx = (_CpuidPx)GetDelegate("CpuidPx", typeof(_CpuidPx));
            Rdtsc = (_Rdtsc)GetDelegate("Rdtsc", typeof(_Rdtsc));
            RdtscTx = (_RdtscTx)GetDelegate("RdtscTx", typeof(_RdtscTx));
            RdtscPx = (_RdtscPx)GetDelegate("RdtscPx", typeof(_RdtscPx));

            ReadIoPortByte = (_ReadIoPortByte)GetDelegate("ReadIoPortByte", typeof(_ReadIoPortByte));
            ReadIoPortWord = (_ReadIoPortWord)GetDelegate("ReadIoPortWord", typeof(_ReadIoPortWord));
            ReadIoPortDword = (_ReadIoPortDword)GetDelegate("ReadIoPortDword", typeof(_ReadIoPortDword));
            ReadIoPortByteEx = (_ReadIoPortByteEx)GetDelegate("ReadIoPortByteEx", typeof(_ReadIoPortByteEx));
            ReadIoPortWordEx = (_ReadIoPortWordEx)GetDelegate("ReadIoPortWordEx", typeof(_ReadIoPortWordEx));
            ReadIoPortDwordEx = (_ReadIoPortDwordEx)GetDelegate("ReadIoPortDwordEx", typeof(_ReadIoPortDwordEx));

            WriteIoPortByte = (_WriteIoPortByte)GetDelegate("WriteIoPortByte", typeof(_WriteIoPortByte));
            WriteIoPortWord = (_WriteIoPortWord)GetDelegate("WriteIoPortWord", typeof(_WriteIoPortWord));
            WriteIoPortDword = (_WriteIoPortDword)GetDelegate("WriteIoPortDword", typeof(_WriteIoPortDword));
            WriteIoPortByteEx = (_WriteIoPortByteEx)GetDelegate("WriteIoPortByteEx", typeof(_WriteIoPortByteEx));
            WriteIoPortWordEx = (_WriteIoPortWordEx)GetDelegate("WriteIoPortWordEx", typeof(_WriteIoPortWordEx));
            WriteIoPortDwordEx = (_WriteIoPortDwordEx)GetDelegate("WriteIoPortDwordEx", typeof(_WriteIoPortDwordEx));

            SetPciMaxBusIndex = (_SetPciMaxBusIndex)GetDelegate("SetPciMaxBusIndex", typeof(_SetPciMaxBusIndex));
            ReadPciConfigByte = (_ReadPciConfigByte)GetDelegate("ReadPciConfigByte", typeof(_ReadPciConfigByte));
            ReadPciConfigWord = (_ReadPciConfigWord)GetDelegate("ReadPciConfigWord", typeof(_ReadPciConfigWord));
            ReadPciConfigDword = (_ReadPciConfigDword)GetDelegate("ReadPciConfigDword", typeof(_ReadPciConfigDword));
            ReadPciConfigByteEx = (_ReadPciConfigByteEx)GetDelegate("ReadPciConfigByteEx", typeof(_ReadPciConfigByteEx));
            ReadPciConfigWordEx = (_ReadPciConfigWordEx)GetDelegate("ReadPciConfigWordEx", typeof(_ReadPciConfigWordEx));
            ReadPciConfigDwordEx = (_ReadPciConfigDwordEx)GetDelegate("ReadPciConfigDwordEx", typeof(_ReadPciConfigDwordEx));
            WritePciConfigByte = (_WritePciConfigByte)GetDelegate("WritePciConfigByte", typeof(_WritePciConfigByte));
            WritePciConfigWord = (_WritePciConfigWord)GetDelegate("WritePciConfigWord", typeof(_WritePciConfigWord));
            WritePciConfigDword = (_WritePciConfigDword)GetDelegate("WritePciConfigDword", typeof(_WritePciConfigDword));
            WritePciConfigByteEx = (_WritePciConfigByteEx)GetDelegate("WritePciConfigByteEx", typeof(_WritePciConfigByteEx));
            WritePciConfigWordEx = (_WritePciConfigWordEx)GetDelegate("WritePciConfigWordEx", typeof(_WritePciConfigWordEx));
            WritePciConfigDwordEx = (_WritePciConfigDwordEx)GetDelegate("WritePciConfigDwordEx", typeof(_WritePciConfigDwordEx));
            FindPciDeviceById = (_FindPciDeviceById)GetDelegate("FindPciDeviceById", typeof(_FindPciDeviceById));
            FindPciDeviceByClass = (_FindPciDeviceByClass)GetDelegate("FindPciDeviceByClass", typeof(_FindPciDeviceByClass));

#if _PHYSICAL_MEMORY_SUPPORT
                ReadDmiMemory = (_ReadDmiMemory)GetDelegate("ReadDmiMemory", typeof(_ReadDmiMemory));
                ReadPhysicalMemory = (_ReadPhysicalMemory)GetDelegate("ReadPhysicalMemory", typeof(_ReadPhysicalMemory));
                WritePhysicalMemory = (_WritePhysicalMemory)GetDelegate("WritePhysicalMemory", typeof(_WritePhysicalMemory));
#endif
            if (!(
               GetDllStatus != null
            && GetDllVersion != null
            && GetDriverVersion != null
            && GetDriverType != null
            && InitializeOls != null
            && DeinitializeOls != null
            && IsCpuid != null
            && IsMsr != null
            && IsTsc != null
            && Hlt != null
            && HltTx != null
            && HltPx != null
            && Rdmsr != null
            && RdmsrTx != null
            && RdmsrPx != null
            && Wrmsr != null
            && WrmsrTx != null
            && WrmsrPx != null
            && Rdpmc != null
            && RdpmcTx != null
            && RdpmcPx != null
            && Cpuid != null
            && CpuidTx != null
            && CpuidPx != null
            && Rdtsc != null
            && RdtscTx != null
            && RdtscPx != null
            && ReadIoPortByte != null
            && ReadIoPortWord != null
            && ReadIoPortDword != null
            && ReadIoPortByteEx != null
            && ReadIoPortWordEx != null
            && ReadIoPortDwordEx != null
            && WriteIoPortByte != null
            && WriteIoPortWord != null
            && WriteIoPortDword != null
            && WriteIoPortByteEx != null
            && WriteIoPortWordEx != null
            && WriteIoPortDwordEx != null
            && SetPciMaxBusIndex != null
            && ReadPciConfigByte != null
            && ReadPciConfigWord != null
            && ReadPciConfigDword != null
            && ReadPciConfigByteEx != null
            && ReadPciConfigWordEx != null
            && ReadPciConfigDwordEx != null
            && WritePciConfigByte != null
            && WritePciConfigWord != null
            && WritePciConfigDword != null
            && WritePciConfigByteEx != null
            && WritePciConfigWordEx != null
            && WritePciConfigDwordEx != null
            && FindPciDeviceById != null
            && FindPciDeviceByClass != null
#if _PHYSICAL_MEMORY_SUPPORT
                && ReadDmiMemory != null
                && ReadPhysicalMemory != null
                && WritePhysicalMemory != null
#endif
            ))
            {
                status = (uint)Status.DLL_INCORRECT_VERSION;
            }

            if (InitializeOls() == 0)
            {
                status = (uint)Status.DLL_INITIALIZE_ERROR;
            }
        }
    }

    public uint GetStatus()
    {
        return status;
    }

    public void Dispose()
    {
        if (module != IntPtr.Zero)
        {
            DeinitializeOls();
            Ols.FreeLibrary(module);
            module = IntPtr.Zero;
        }
    }

    public Delegate GetDelegate(string procName, Type delegateType)
    {
        var ptr = GetProcAddress(module, procName);
        if (ptr != IntPtr.Zero)
        {
            var d = Marshal.GetDelegateForFunctionPointer(ptr, delegateType);
            return d;
        }

        var result = Marshal.GetHRForLastWin32Error();
        throw Marshal.GetExceptionForHR(result);
    }

    //-----------------------------------------------------------------------------
    // DLL Information
    //-----------------------------------------------------------------------------
    public delegate uint _GetDllStatus();
    public delegate uint _GetDllVersion(ref byte major, ref byte minor, ref byte revision, ref byte release);
    public delegate uint _GetDriverVersion(ref byte major, ref byte minor, ref byte revision, ref byte release);
    public delegate uint _GetDriverType();

    public delegate int _InitializeOls();
    public delegate void _DeinitializeOls();

    public _GetDllStatus GetDllStatus = null;
    public _GetDriverType GetDriverType = null;
    public _GetDllVersion GetDllVersion = null;
    public _GetDriverVersion GetDriverVersion = null;

    public _InitializeOls InitializeOls = null;
    public _DeinitializeOls DeinitializeOls = null;

    //-----------------------------------------------------------------------------
    // CPU
    //-----------------------------------------------------------------------------
    public delegate int _IsCpuid();
    public delegate int _IsMsr();
    public delegate int _IsTsc();
    public delegate int _Hlt();
    public delegate int _HltTx(UIntPtr threadAffinityMask);
    public delegate int _HltPx(UIntPtr processAffinityMask);
    public delegate int _Rdmsr(uint index, ref uint eax, ref uint edx);
    public delegate int _RdmsrTx(uint index, ref uint eax, ref uint edx, UIntPtr threadAffinityMask);
    public delegate int _RdmsrPx(uint index, ref uint eax, ref uint edx, UIntPtr processAffinityMask);
    public delegate int _Wrmsr(uint index, uint eax, uint edx);
    public delegate int _WrmsrTx(uint index, uint eax, uint edx, UIntPtr threadAffinityMask);
    public delegate int _WrmsrPx(uint index, uint eax, uint edx, UIntPtr processAffinityMask);
    public delegate int _Rdpmc(uint index, ref uint eax, ref uint edx);
    public delegate int _RdpmcTx(uint index, ref uint eax, ref uint edx, UIntPtr threadAffinityMask);
    public delegate int _RdpmcPx(uint index, ref uint eax, ref uint edx, UIntPtr processAffinityMask);
    public delegate int _Cpuid(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx);
    public delegate int _CpuidTx(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx, UIntPtr threadAffinityMask);
    public delegate int _CpuidPx(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx, UIntPtr processAffinityMask);
    public delegate int _Rdtsc(ref uint eax, ref uint edx);
    public delegate int _RdtscTx(ref uint eax, ref uint edx, UIntPtr threadAffinityMask);
    public delegate int _RdtscPx(ref uint eax, ref uint edx, UIntPtr processAffinityMask);

    public _IsCpuid IsCpuid = null;
    public _IsMsr IsMsr = null;
    public _IsTsc IsTsc = null;
    public _Hlt Hlt = null;
    public _HltTx HltTx = null;
    public _HltPx HltPx = null;
    public _Rdmsr Rdmsr = null;
    public _RdmsrTx RdmsrTx = null;
    public _RdmsrPx RdmsrPx = null;
    public _Wrmsr Wrmsr = null;
    public _WrmsrTx WrmsrTx = null;
    public _WrmsrPx WrmsrPx = null;
    public _Rdpmc Rdpmc = null;
    public _RdpmcTx RdpmcTx = null;
    public _RdpmcPx RdpmcPx = null;
    public _Cpuid Cpuid = null;
    public _CpuidTx CpuidTx = null;
    public _CpuidPx CpuidPx = null;
    public _Rdtsc Rdtsc = null;
    public _RdtscTx RdtscTx = null;
    public _RdtscPx RdtscPx = null;

    //-----------------------------------------------------------------------------
    // I/O
    //-----------------------------------------------------------------------------
    public delegate byte _ReadIoPortByte(ushort port);
    public delegate ushort _ReadIoPortWord(ushort port);
    public delegate uint _ReadIoPortDword(ushort port);
    public _ReadIoPortByte ReadIoPortByte;
    public _ReadIoPortWord ReadIoPortWord;
    public _ReadIoPortDword ReadIoPortDword;

    public delegate int _ReadIoPortByteEx(ushort port, ref byte value);
    public delegate int _ReadIoPortWordEx(ushort port, ref ushort value);
    public delegate int _ReadIoPortDwordEx(ushort port, ref uint value);
    public _ReadIoPortByteEx ReadIoPortByteEx;
    public _ReadIoPortWordEx ReadIoPortWordEx;
    public _ReadIoPortDwordEx ReadIoPortDwordEx;

    public delegate void _WriteIoPortByte(ushort port, byte value);
    public delegate void _WriteIoPortWord(ushort port, ushort value);
    public delegate void _WriteIoPortDword(ushort port, uint value);
    public _WriteIoPortByte WriteIoPortByte;
    public _WriteIoPortWord WriteIoPortWord;
    public _WriteIoPortDword WriteIoPortDword;

    public delegate int _WriteIoPortByteEx(ushort port, byte value);
    public delegate int _WriteIoPortWordEx(ushort port, ushort value);
    public delegate int _WriteIoPortDwordEx(ushort port, uint value);
    public _WriteIoPortByteEx WriteIoPortByteEx;
    public _WriteIoPortWordEx WriteIoPortWordEx;
    public _WriteIoPortDwordEx WriteIoPortDwordEx;

    //-----------------------------------------------------------------------------
    // PCI
    //-----------------------------------------------------------------------------
    public delegate void _SetPciMaxBusIndex(byte max);
    public _SetPciMaxBusIndex SetPciMaxBusIndex;

    public delegate byte _ReadPciConfigByte(uint pciAddress, byte regAddress);
    public delegate ushort _ReadPciConfigWord(uint pciAddress, byte regAddress);
    public delegate uint _ReadPciConfigDword(uint pciAddress, byte regAddress);
    public _ReadPciConfigByte ReadPciConfigByte;
    public _ReadPciConfigWord ReadPciConfigWord;
    public _ReadPciConfigDword ReadPciConfigDword;

    public delegate int _ReadPciConfigByteEx(uint pciAddress, uint regAddress, ref byte value);
    public delegate int _ReadPciConfigWordEx(uint pciAddress, uint regAddress, ref ushort value);
    public delegate int _ReadPciConfigDwordEx(uint pciAddress, uint regAddress, ref uint value);
    public _ReadPciConfigByteEx ReadPciConfigByteEx;
    public _ReadPciConfigWordEx ReadPciConfigWordEx;
    public _ReadPciConfigDwordEx ReadPciConfigDwordEx;

    public delegate void _WritePciConfigByte(uint pciAddress, byte regAddress, byte value);
    public delegate void _WritePciConfigWord(uint pciAddress, byte regAddress, ushort value);
    public delegate void _WritePciConfigDword(uint pciAddress, byte regAddress, uint value);
    public _WritePciConfigByte WritePciConfigByte;
    public _WritePciConfigWord WritePciConfigWord;
    public _WritePciConfigDword WritePciConfigDword;

    public delegate int _WritePciConfigByteEx(uint pciAddress, uint regAddress, byte value);
    public delegate int _WritePciConfigWordEx(uint pciAddress, uint regAddress, ushort value);
    public delegate int _WritePciConfigDwordEx(uint pciAddress, uint regAddress, uint value);
    public _WritePciConfigByteEx WritePciConfigByteEx;
    public _WritePciConfigWordEx WritePciConfigWordEx;
    public _WritePciConfigDwordEx WritePciConfigDwordEx;

    public delegate uint _FindPciDeviceById(ushort vendorId, ushort deviceId, byte index);
    public delegate uint _FindPciDeviceByClass(byte baseClass, byte subClass, byte programIf, byte index);
    public _FindPciDeviceById FindPciDeviceById;
    public _FindPciDeviceByClass FindPciDeviceByClass;

    //-----------------------------------------------------------------------------
    // Physical Memory (unsafe)
    //-----------------------------------------------------------------------------
#if _PHYSICAL_MEMORY_SUPPORT
        public unsafe delegate uint _ReadDmiMemory(byte* buffer, uint count, uint unitSize);
        public _ReadDmiMemory ReadDmiMemory;

        public unsafe delegate uint _ReadPhysicalMemory(UIntPtr address, byte* buffer, uint count, uint unitSize);
        public unsafe delegate uint _WritePhysicalMemory(UIntPtr address, byte* buffer, uint count, uint unitSize);

        public _ReadPhysicalMemory ReadPhysicalMemory;
        public _WritePhysicalMemory WritePhysicalMemory;
#endif


    private static readonly int _numCores = System.Environment.ProcessorCount;

    /// <summary>
    /// Reads from a 64 bit MSR of the current CPU core.
    /// </summary>
    /// <param name="index">MSR index/address.</param>
    public ulong ReadMsr(uint index)
    {
        uint lower = 0, upper = 0;
        if (Rdmsr(index, ref lower, ref upper) == 0)
            throw new NotSupportedException("Could not read from MSR.");

        return ((ulong)upper << 32) | lower;
    }

    /// <summary>
    /// Reads from a 64 bit MSR of a specific CPU core.
    /// </summary>
    /// <param name="index">MSR index/address.</param>
    /// <param name="core">Index of the CPU core.</param>
    public ulong ReadMsr(uint index, int core)
    {
        if (core < 0 || core >= _numCores)
            throw new ArgumentOutOfRangeException("core");

        uint lower = 0, upper = 0;
        if (RdmsrTx(index, ref lower, ref upper, new UIntPtr(1u << core)) == 0)
            throw new NotSupportedException("Could not read from MSR.");

        return ((ulong)upper << 32) | lower;
    }

    /// <summary>
    /// Writes to a 64 bit MSR of the current CPU core.
    /// </summary>
    /// <param name="index">MSR index/address.</param>
    /// <param name="value">Value to be written.</param>
    public void WriteMsr(uint index, ulong value)
    {
        var lower = (uint)(value & 0xFFFFFFFFu);
        var upper = (uint)(value >> 32);

        if (Wrmsr(index, lower, upper) == 0)
            throw new NotSupportedException("Could not write to MSR.");
    }

    /// <summary>
    /// Writes to a 64 bit MSR of a specific CPU core.
    /// </summary>
    /// <param name="index">MSR index/address.</param>
    /// <param name="value">Value to be written.</param>
    /// <param name="core">Index of the CPU core.</param>
    public void WriteMsr(uint index, ulong value, int core)
    {
        if (core < 0 || core >= _numCores)
            throw new ArgumentOutOfRangeException("core");

        var lower = (uint)(value & 0xFFFFFFFFu);
        var upper = (uint)(value >> 32);

        if (WrmsrTx(index, lower, upper, new UIntPtr(1u << core)) == 0)
            throw new NotSupportedException("Could not write to MSR.");
    }


    /// <summary>
    /// Reads from a 32 bit PCI register.
    /// </summary>
    /// <param name="pciAddress"></param>
    /// <param name="registerAddress"></param>
    /// <returns></returns>
    public uint ReadPciConfig(uint pciAddress, uint registerAddress)
    {
        uint settings = 0;
        if (ReadPciConfigDwordEx(pciAddress, registerAddress, ref settings) == 0)
            throw new NotSupportedException("Could not read from PCI register.");

        return settings;
    }

    /// <summary>
    /// Writes to a 32 bit PCI register.
    /// </summary>
    /// <param name="pciAddress"></param>
    /// <param name="registerAddress"></param>
    /// <param name="value"></param>
    public void WritePciConfig(uint pciAddress, uint registerAddress, uint value)
    {
        if (WritePciConfigDwordEx(pciAddress, registerAddress, value) == 0)
            throw new NotSupportedException("Could not write to PCI register.");
    }
}
