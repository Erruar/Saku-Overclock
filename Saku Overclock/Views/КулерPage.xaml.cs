using System.Diagnostics;
using System.Globalization;
using Windows.Foundation.Metadata;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation; 
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;

namespace Saku_Overclock.Views;

public sealed partial class КулерPage
{ 
    private bool _isPageLoaded;
    private bool _isNbfcNotLoaded;
    private bool _doNotUseRyzenAdj;
    private IntPtr _ry = IntPtr.Zero;
    private DispatcherTimer? _tempUpdateTimer;
    private readonly DispatcherTimer? _fanUpdateTimer;
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();

    public КулерPage()
    {
        App.GetService<КулерViewModel>();
        InitializeComponent(); 
        FanInit();
        Update();
        SettingsService.FlagRyzenADJConsoleTemperatureCheckRunning =
            true; //Автообновление информации о кулере включено! Это нужно для того, чтобы обновление информации не происходило нигде кроме страницы с оптимизацией кулера, так как контроллировать асинхронные методы бывает сложно
        SettingsService.SaveSettings();
        Loaded += Page_Loaded;
        _fanUpdateTimer = new DispatcherTimer();
        _fanUpdateTimer.Tick += async (_, _) => await CheckFan();
        _fanUpdateTimer.Interval = TimeSpan.FromMilliseconds(6000);
        Unloaded += Page_Unloaded;
    }

    #region Page Navigation and Window State

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.CodeActivated ||
            args.WindowActivationState == WindowActivationState.PointerActivated)
        {
            _tempUpdateTimer?.Start();
            if (Fanauto.IsChecked == true)
            {
                _fanUpdateTimer?.Start();
            }
        }
        else
        {
            _tempUpdateTimer?.Stop();
            _fanUpdateTimer?.Stop();
        }
    }

    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            _tempUpdateTimer?.Start();
            if (Fanauto.IsChecked == true)
            {
                _fanUpdateTimer?.Start();
            }
        }
        else
        {
            _tempUpdateTimer?.Stop();
            _fanUpdateTimer?.Stop();
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StartTempUpdate();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        StopTempUpdate(false);
    }

    private void AdvancedCooler_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(AdvancedКулерViewModel).FullName!);
    }

    private void AsusCoolerMode_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(AsusКулерViewModel).FullName!);
    }

    #endregion

    #region Initialization 
    private async void FanInit()
    {
        try
        { 
            try
            {
                const string
                    folderPath =
                        @"C:\Program Files (x86)\NoteBook FanControl\Configs"; // Получить папку, в которой хранятся файлы XML с конфигами
                var xmlFiles = Directory.GetFiles(folderPath, "*.xml");
                Selfan.Items.Clear();
                foreach (var xmlFile in xmlFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(xmlFile);
                    if (fileName.Contains(".xml"))
                    {
                        fileName = fileName.Replace(".xml", "");
                    }

                    var item = new ComboBoxItem
                    {
                        Content = fileName,
                        Tag = xmlFile
                    };
                    Selfan.Items.Add(item);
                    if (SettingsService.NBFCConfigXMLName == fileName)
                    {
                        Selfan.SelectedItem = item;
                    }
                }
            }
            catch
            {
                _isNbfcNotLoaded = true;
                if (!_isPageLoaded)
                {
                    return;
                }

                await ShowNbfcDialogAsync();
            }

            if (SettingsService.NBFCServiceStatusEnabled)
            { 
                Fan1.Value = SettingsService.NBFCFan1UserFanSpeedRPM;
                Fan2.Value = SettingsService.NBFCFan2UserFanSpeedRPM;
                Enabl.IsChecked = true;
                Readon.IsChecked = false;
                Disabl.IsChecked = false;
                Fan1Val.Text = Fan1.Value.ToString(CultureInfo.InvariantCulture) + " %";
                Fan2Val.Text = Fan2.Value.ToString(CultureInfo.InvariantCulture) + " %";
                if (Fan1.Value > 100)
                {
                    Fan1Val.Text = "Auto";
                }

                if (Fan2.Value > 100)
                {
                    Fan2Val.Text = "Auto";
                }
            }

            if (SettingsService.NBFCServiceStatusReadOnly)
            {
                Enabl.IsChecked = false;
                Readon.IsChecked = true;
                Disabl.IsChecked = false;
            }

            if (SettingsService.NBFCServiceStatusDisabled)
            {
                Enabl.IsChecked = false;
                Readon.IsChecked = false;
                Disabl.IsChecked = true;
            }

            if (SettingsService.NBFCAutoUpdateInformation)
            {
                await Task.Delay(20);
                Fanauto.IsChecked = true;
            }

            if (Enabl.IsChecked == true)
            {
                NbfcFan1();
                if (Fan1.Value > 100)
                {
                    Fan1Val.Text = "Auto";
                    Update();
                    SettingsService.NBFCFan1UserFanSpeedRPM = 110.0;
                }
                else
                {
                    Fan1Pr.Value = Fan1.Value;
                    Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                    SettingsService.NBFCFlagConsoleCheckSpeedRunning = false;
                }

                SettingsService.SaveSettings();
            }

            if (Readon.IsChecked == true)
            {
                Fan1Val.Text = "Auto";
                Update();
            }
        }
        catch (Exception e)
        {
            SendSmuCommand.TraceIt_TraceError(e.ToString());
        }
    }

    // Метод для отображения диалога и загрузки NBFC
    private async Task ShowNbfcDialogAsync()
    {
        // Создаем элементы интерфейса, которые понадобятся в диалоге
        var downloadButton = new Button
        {
            Margin = new Thickness(0, 12, 0, 0),
            CornerRadius = new CornerRadius(15),
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Children =
                {
                    new FontIcon { Glyph = "\uE74B" }, // Иконка загрузки
                    new TextBlock
                    {
                        Margin = new Thickness(10, 0, 0, 0), Text = "Cooler_DownloadNBFC_Title".GetLocalized(),
                        FontWeight = new FontWeight(700)
                    }
                }
            },
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var progressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Opacity = 0.0,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Cooler_DownloadNBFC_Desc".GetLocalized(),
                    Width = 300,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Left
                },
                downloadButton,
                progressBar
            }
        };

        var themerDialog = new ContentDialog
        {
            Title = "Warning".GetLocalized(),
            Content = stackPanel,
            CloseButtonText = "Cancel".GetLocalized(),
            PrimaryButtonText = "Next".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false // Первоначально кнопка "Далее" неактивна
        };

        if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
        {
            themerDialog.XamlRoot = XamlRoot;
        }

        // Обработчик события нажатия на кнопку загрузки
        downloadButton.Click += async (_, _) =>
        {
            downloadButton.IsEnabled = false;
            progressBar.Opacity = 1.0;

            var client = new GitHubClient(new ProductHeaderValue("SakuOverclock"));
            var releases = await client.Repository.Release.GetAll("hirschmann", "nbfc");
            var latestRelease = releases[0];

            var downloadUrl = latestRelease.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe"))?.BrowserDownloadUrl;
            if (downloadUrl != null)
            {
                var httpClient = new HttpClient();
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 1;
                var downloadPath = Path.Combine(Path.GetTempPath(), "NBFC");

                var fileStream = new FileStream(downloadPath, FileMode.Create, FileAccess.Write,
                    FileShare.None);
                var downloadStream = await response.Content.ReadAsStreamAsync();
                var buffer = new byte[8192];
                int bytesRead;
                long totalRead = 0;

                while ((bytesRead = await downloadStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    progressBar.Value = (double)totalRead / totalBytes * 100;
                }

                await Task.Delay(1000); // Задержка в 1 секунду
                // Убедиться, что файл полностью закрыт перед запуском
                if (File.Exists(downloadPath))
                {
                    label_8:
                    try
                    {
                        // Запуск загруженного установочного файла с правами администратора
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = downloadPath,
                            Verb = "runas" // Запуск от имени администратора
                        });
                    }
                    catch (Exception ex)
                    {
                        await App.MainWindow.ShowMessageDialogAsync(
                            "Cooler_DownloadNBFC_ErrorDesc".GetLocalized() + $": {ex.Message}", "Error".GetLocalized());
                        await Task.Delay(2000);
                        goto
                            label_8; // Повторить задачу открытия автообновления приложения, в случае если возникла ошибка доступа
                    }
                }

                downloadButton.Opacity = 0.0;
                progressBar.Opacity = 0.0;
                // Изменение текста диалога и активация кнопки "Далее"
                themerDialog.Content = new TextBlock
                {
                    Text = "Cooler_DownloadNBFC_AfterDesc".GetLocalized(),
                    TextAlignment = TextAlignment.Center
                };
                themerDialog.IsPrimaryButtonEnabled = true;
            }
        };
        var result = await themerDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            PageService.ReloadPage(typeof(КулерViewModel).FullName!); // Вызов метода перезагрузки страницы
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isPageLoaded = true;
            _ry = RyzenADJWrapper.Init_ryzenadj();
            RyzenADJWrapper.Init_Table(_ry);
            if (_isNbfcNotLoaded)
            {
                await ShowNbfcDialogAsync();
            }
        }
        catch (Exception xException)
        {
            SendSmuCommand.TraceIt_TraceError(xException.ToString());
        }
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e) => StopTempUpdate(true);

    #endregion

    #region Event Handlers

    private void Disabl_Checked(object sender, RoutedEventArgs e)
    {
        SettingsService.NBFCServiceStatusDisabled = true;
        SettingsService.NBFCServiceStatusReadOnly = false;
        SettingsService.NBFCServiceStatusEnabled = false;
        SettingsService.NBFCFlagConsoleCheckSpeedRunning = false;
        SettingsService.SaveSettings();
        NbfcEnable();
    }

    private void Readon_Checked(object sender, RoutedEventArgs e)
    {
        SettingsService.NBFCServiceStatusReadOnly = true;
        SettingsService.NBFCServiceStatusEnabled = false;
        SettingsService.NBFCServiceStatusDisabled = false;
        SettingsService.SaveSettings();
        NbfcEnable();
        Fan1Val.Text = "Auto";
        Update();
    }

    private void Enabl_Checked(object sender, RoutedEventArgs e)
    {
        SettingsService.NBFCServiceStatusEnabled = true;
        SettingsService.NBFCServiceStatusDisabled = false;
        SettingsService.NBFCServiceStatusReadOnly = false;
        SettingsService.SaveSettings();
        NbfcEnable();
    }

    private async void Fan1_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            if (Enabl.IsChecked == true)
            {
                NbfcFan1();
                await Task.Delay(200);
                SettingsService.NBFCFan1UserFanSpeedRPM = Fan1.Value;
                Fan1Val.Text = Fan1.Value.ToString(CultureInfo.InvariantCulture) + " %";
                if (Fan1.Value > 100)
                {
                    Fan1Val.Text = "Auto";
                    Update();
                    SettingsService.NBFCFan1UserFanSpeedRPM = 110.0;
                }
                else
                {
                    Fan1Pr.Value = Fan1.Value;
                    Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                    SettingsService.NBFCFlagConsoleCheckSpeedRunning = false;
                }

                SettingsService.SaveSettings();
            }

            if (Readon.IsChecked == true)
            {
                Fan1Val.Text = "Auto";
                Update();
            }
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Fan2_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        try
        {
            if (Enabl.IsChecked == true)
            {
                NbfcFan2();
                await Task.Delay(200);
                SettingsService.NBFCFan2UserFanSpeedRPM = Fan2.Value;
                Fan2Val.Text = Fan2.Value.ToString(CultureInfo.InvariantCulture) + " %";
                if (Fan2.Value > 100)
                {
                    Fan2Val.Text = "Auto";
                    Update();
                    SettingsService.NBFCFan2UserFanSpeedRPM = 110.0; 
                    if (Fan1Pr.Value - 10.0d == 0.0d)
                    {
                        Fan1Pr.Value = 100;
                    }
                }
                else
                {
                    Fan2Pr.Value = Fan2.Value;
                    Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value;
                    SettingsService.NBFCFlagConsoleCheckSpeedRunning = false; 
                }

                SettingsService.SaveSettings();
            }

            if (Readon.IsChecked == true)
            {
                Fan2Val.Text = "Auto";
                Update();
            }
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Selfan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            if (!_isPageLoaded)
            {
                return;
            }

            await Task.Delay(200);
            SettingsService.NBFCConfigXMLName = (string)((ComboBoxItem)Selfan.SelectedItem).Content;
            SettingsService.SaveSettings();
            NbfcFanState();
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Fanauto_Checked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Fanauto.IsChecked == true)
            {
                if (SettingsService.NBFCAutoUpdateInformation == false)
                {
                    var autoDialog = new ContentDialog
                    {
                        Title = "Cooler_FanAuto_Text".GetLocalized(),
                        Content = "Cooler_FanAuto_Desc".GetLocalized(),
                        CloseButtonText = "Cancel".GetLocalized(),
                        PrimaryButtonText = "Enable".GetLocalized(),
                        DefaultButton = ContentDialogButton.Close
                    };
                    // Use this code to associate the dialog to the appropriate AppWindow by setting
                    // the dialog's XamlRoot to the same XamlRoot as an element that is already present in the AppWindow.
                    if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                    {
                        autoDialog.XamlRoot = XamlRoot;
                    }

                    var result = await autoDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        SettingsService.NBFCAutoUpdateInformation = true; 
                    }
                    else
                    {
                        Fanauto.IsChecked = false;
                        SettingsService.NBFCAutoUpdateInformation = false; 
                    }
                }

                SettingsService.NBFCFlagConsoleCheckSpeedRunning = false; 
                GetInfo0(true);
            }
            else
            {
                SettingsService.NBFCAutoUpdateInformation = false; 
                GetInfo0(false);
            }
            SettingsService.SaveSettings();
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            SettingsService.NBFCFlagConsoleCheckSpeedRunning = true;
            SettingsService.SaveSettings();
            await CheckFan();
            Fanauto.IsChecked = false;
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    private async void Suggest_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SuggestClickAsync();
        }
        catch (Exception exception)
        {
            SendSmuCommand.TraceIt_TraceError(exception.ToString());
        }
    }

    #endregion

    #region NBFC Tasks

    private void NbfcEnable()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = "nbfc/nbfc.exe"; 
        if (SettingsService.NBFCServiceStatusDisabled)
        {
            p.StartInfo.Arguments = " stop";
        }

        if (SettingsService.NBFCServiceStatusEnabled)
        {
            p.StartInfo.Arguments = " start --enabled";
        }

        if (SettingsService.NBFCServiceStatusReadOnly)
        {
            p.StartInfo.Arguments = " start --readonly";
        }

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void NbfcFan1()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe"; 
        if (SettingsService.NBFCServiceStatusEnabled)
        {
            if (Fan1.Value < 100)
            {
                p.StartInfo.Arguments = " set --fan 0 --speed " + Fan1.Value;
            }
            else
            {
                p.StartInfo.Arguments = " set --fan 0 --auto";
            }
        }

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void NbfcFan2()
    {
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe"; 
        if (SettingsService.NBFCServiceStatusEnabled)
        {
            if (Fan2.Value < 100)
            {
                p.StartInfo.Arguments = " set --fan 1 --speed " + Fan2.Value;
            }
            else
            {
                p.StartInfo.Arguments = " set --fan 1 --auto";
            }
        }

        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void NbfcFanState()
    {
        const string quote = "\"";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe"; 
        p.StartInfo.Arguments = " config --apply " + quote + SettingsService.NBFCConfigXMLName + quote;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
    }

    private void GetInfo0(bool start)
    {
        SettingsService.NBFCFlagConsoleCheckSpeedRunning = true;
        SettingsService.SaveSettings();
        if (start)
        {
            _fanUpdateTimer?.Start();
        }
        else
        {
            _fanUpdateTimer?.Stop();
        }
    }

    private async Task CheckFan()
    {
        if (Readon.IsChecked == true || Enabl.IsChecked == true)
        {
            if (SettingsService.NBFCFlagConsoleCheckSpeedRunning)
            {
                SettingsService.NBFCAnswerSpeedFan1 = "";
                SettingsService.NBFCAnswerSpeedFan2 = "";
                var p = new Process();
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.FileName = @"nbfc/nbfc.exe";
                p.StartInfo.Arguments = " status --fan 0";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                try
                {
                    p.Start();
                }
                catch (Exception ex)
                {
                    await App.MainWindow.ShowMessageDialogAsync(
                        "Error " + ex.Message + " of КулерPage.xaml.cs in com.sakuoverclock.org", "Critical error!");
                }

                var outputWriter = p.StandardOutput;
                var line = await outputWriter.ReadLineAsync();
                while (line != null)
                {
                    if (line.Contains("Current fan speed"))
                    {
                        SettingsService.NBFCAnswerSpeedFan1 = line.Replace("Current fan speed", "").Replace(" ", "")
                            .Replace(":", "").Replace("\t", "");
                        SettingsService.SaveSettings();
                    }

                    line = await outputWriter.ReadLineAsync();
                }

                await p.WaitForExitAsync();
                var p1 = new Process(); //fan 2
                p1.StartInfo.UseShellExecute = false;
                p1.StartInfo.FileName = @"nbfc/nbfc.exe";
                p1.StartInfo.Arguments = " status --fan 1";
                p1.StartInfo.CreateNoWindow = true;
                p1.StartInfo.RedirectStandardError = true;
                p1.StartInfo.RedirectStandardInput = true;
                p1.StartInfo.RedirectStandardOutput = true;
                p1.Start();
                var outputWriter1 = p1.StandardOutput;
                var line1 = await outputWriter1.ReadLineAsync();
                while (line1 != null)
                {
                    if (line1.Contains("Current fan speed"))
                    {
                        SettingsService.NBFCAnswerSpeedFan2 = line1.Replace("Current fan speed", "").Replace(" ", "")
                            .Replace(":", "").Replace("\t", "");
                        SettingsService.SaveSettings();
                    }

                    line1 = await outputWriter1.ReadLineAsync();
                }

                await p1.WaitForExitAsync();
                Update();
            }
        }
    }

    private void Update()
    { 
        if (SettingsService.NBFCAnswerSpeedFan1 == string.Empty)
        {
            return;
        }

        try
        {
            Fan1Pr.Value = Convert.ToInt32(double.Parse(SettingsService.NBFCAnswerSpeedFan1, CultureInfo.InvariantCulture));
            if (Fan1Pr.Value > 100)
            {
                Fan1Pr.Value = 100;
            }

            Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + SettingsService.NBFCAnswerSpeedFan1 + "%";
        }
        catch
        {
            if (Fan1.Value < 100)
            {
                Fan1Pr.Value = Fan1.Value;
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value + "%";
            }
        }

        if (SettingsService.NBFCAnswerSpeedFan2 == string.Empty)
        {
            return;
        }

        try
        {
            Fan2Pr.Value = Convert.ToInt32(double.Parse(SettingsService.NBFCAnswerSpeedFan2, CultureInfo.InvariantCulture));
            if (Fan2Pr.Value > 100)
            {
                Fan2Pr.Value = 100;
            }

            Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + SettingsService.NBFCAnswerSpeedFan2 + "%";
        }
        catch
        {
            if (Fan2.Value < 100)
            {
                Fan2Pr.Value = Fan2.Value;
                Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value + "%";
            }
        }
    }

    private async Task SuggestClickAsync()
    {
        SuggestTip.Subtitle = "";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        p.StartInfo.Arguments = " config -r";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        try
        {
            p.Start();
        }
        catch (Exception ex)
        {
            SendSmuCommand.TraceIt_TraceError(ex.ToString());
        }

        var outputWriter = p.StandardOutput;
        var line = await outputWriter.ReadLineAsync();
        while (line != null)
        {
            if (line != "")
            {
                SuggestTip.Subtitle += line + "\n";
            }

            line = await outputWriter.ReadLineAsync();
        }

        await p.WaitForExitAsync();
        SuggestTip.IsOpen = true;
    }

    private void StartTempUpdate()
    {
        _tempUpdateTimer = new DispatcherTimer();
        _tempUpdateTimer.Tick += (_, _) => UpdateTemperatureAsync();
        _tempUpdateTimer.Interval = TimeSpan.FromMilliseconds(500);
        App.MainWindow.Activated += Window_Activated; //Проверка фокуса на программе для экономии ресурсов
        App.MainWindow.VisibilityChanged +=
            Window_VisibilityChanged; //Проверка программу на трей меню для экономии ресурсов
        _tempUpdateTimer.Start();
    }

    private void UpdateTemperatureAsync()
    {
        _ry = RyzenADJWrapper.Init_ryzenadj();
        if (_ry == 0x0 || _doNotUseRyzenAdj)
        {
            _doNotUseRyzenAdj = true;
            Temp.Text = "?℃";
            return;
        }

        _ = RyzenADJWrapper.Init_Table(_ry);
        _ = RyzenADJWrapper.Refresh_table(_ry);
        Temp.Text = Math.Round(RyzenADJWrapper.Get_tctl_temp_value(_ry), 3) + "℃";
    }

    private void StopTempUpdate(bool exit)
    {
        if (exit)
        {
            RyzenADJWrapper.Cleanup_ryzenadj(_ry);
        }

        _tempUpdateTimer?.Stop();
    }

    #endregion
}