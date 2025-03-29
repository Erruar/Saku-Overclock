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
        _isLoaded = true;
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
        if (!IsLoaded) { return; }
        AppSettings.CurveOptimizerOverallEnabled = O1.IsOn;
        AppSettings.CurveOptimizerOverallLevel = O1m.SelectedIndex;
        AppSettings.SaveSettings();
    }

    private void O1m_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) { return; }
        AppSettings.CurveOptimizerOverallEnabled = O1.IsOn;
        AppSettings.CurveOptimizerOverallLevel = O1m.SelectedIndex;
        AppSettings.SaveSettings();
    }

    private void RtssOverlaySwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) { return; }
        AppSettings.RTSSMetricsEnabled = RtssOverlaySwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void TrayMonFeatSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) { return; }
        AppSettings.NiIconsEnabled = TrayMonFeatSwitch.IsOn;
        AppSettings.SaveSettings();
    }

    private void StreamOptimizerSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) { return; }
        AppSettings.StreamOptimizerEnabled = StreamOptimizerSwitch.IsOn;
        AppSettings.SaveSettings();
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

    private void HideOptionsVizualizer_Click(object sender, RoutedEventArgs e)
    {
        var isNotShown = HideOptionsVizualizerGlyph.Glyph == "\uE710"; // Символ Плюсика +
        OptionsVizualizerRow.Height = isNotShown ? new GridLength(108) : GridLength.Auto;
        SetOptionsVisualizer.Visibility = isNotShown ? Visibility.Visible : Visibility.Collapsed;
        HideOptionsVizualizerGlyph.Glyph = isNotShown ? "\uE738" : "\uE710";
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