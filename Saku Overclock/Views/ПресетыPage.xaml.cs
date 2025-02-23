using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ПресетыPage
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private bool _isLoaded; // Загружена ли корректно страница для применения изменений 
    private bool _waitforload = true; // Ожидание окончательной смены профиля на другой. Активируется при смене профиля 
    private static Profile[] _profile = new Profile[1]; // Всегда по умолчанию будет 1 профиль
    private readonly int _indexprofile = 0; // Выбранный профиль
    private PeriodicTimer? _flipTimer;
    private readonly Random _random = new();

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
        // Таймер для смены страниц раз в 10 секунд
        _flipTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await _flipTimer.WaitForNextTickAsync())
        {
            SwitchRandomPage();
        }
    }

    #region JSON and Initialization

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

    #region Event Handlers

    private void SwitchRandomPage()
    {
        if (TipsFlipView.Items.Count > 1)
        {
            var randomIndex = _random.Next(0, Tips.Count);
            TipsFlipView.SelectedIndex = randomIndex;
        }
    }

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

    private void ProfilesControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //await App.MainWindow.ShowMessageDialogAsync((sender as Saku_Overclock.Styles.ProfileSelector).SelectedItem.Text, "Selected:");
    }

    private void TryAdvancedButton_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

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

    private void HideOptionsVizualizer_Click(object sender, RoutedEventArgs e)
    {
        var isNotShown = HideOptionsVizualizerGlyph.Glyph == "\uE710"; // Символ Плюсика +
        OptionsVizualizerRow.Height = isNotShown ? new GridLength(108) : GridLength.Auto;
        SetOptionsVisualizer.Visibility = isNotShown ? Visibility.Visible : Visibility.Collapsed;
        HideOptionsVizualizerGlyph.Glyph = isNotShown ? "\uE738" : "\uE710";
    }
}