using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Xml.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes; 
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers; 
using Saku_Overclock.SMUEngine;
using Saku_Overclock.Styles;
using Saku_Overclock.ViewModels;
using Path = System.IO.Path;
using TextConstants = Microsoft.UI.Text.TextConstants;
using TextGetOptions = Microsoft.UI.Text.TextGetOptions;
using TextSetOptions = Microsoft.UI.Text.TextSetOptions;
using Octokit;
using Saku_Overclock.Services;
using System.Diagnostics;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;

namespace Saku_Overclock.Views;

public sealed partial class AdvancedКулерPage
{
    private Point _cursorPosition; // Точка, для отображения Flyout, чтобы его точно расположить при нажатии на кнопку +
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();

    public AdvancedКулерPage()
    {
        InitializeComponent();

        SettingsService.NbfcFlagConsoleCheckSpeedRunning =
            false; // Старые флаги для выключения автообновления информации в фоне программы
        SettingsService.FlagRyzenAdjConsoleTemperatureCheckRunning = false;
        SettingsService.SaveSettings();

        Loaded += AdvancedКулерPage_Loaded;
    }

    private void AdvancedКулерPage_Loaded(object sender, RoutedEventArgs e)
    {
        Load_example(); // Загрузка примера из файла
        LoadFanCurvesFromConfig(); // Загрузить кривые
        Init_Configs(); // Инициализация конфигов NBFC
    }

    #region Initialization
    private void Init_Configs() // Инициализация конфигов NBFC
    {
        // Найти XML конфиги
        const string folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\"; // Путь к NBFC
        if (!File.Exists(folderPath))
        {
            CurveFan1.Visibility = Visibility.Collapsed;
            CurveFan2.Visibility = Visibility.Collapsed;
            return;
        } // Если путь не существует - не продолжать инициализуцию

        // Получить все XML-файлы в этой папке
        var xmlFiles = Directory.GetFiles(folderPath, "*.xml"); // Все xml файлы
        Others_CC.Items.Clear(); // Очистить старые элементы настройки кривой
        foreach (var xmlFile in xmlFiles)
        {
            var fileItem = new MenuFlyoutItem
            {
                Text = Path.GetFileName(xmlFile),
                Command = new RelayCommand<string>(HandleFileItemClick!),
                CommandParameter = xmlFile
            };
            var copyItem = new MenuFlyoutItem
            {
                Text = Path.GetFileName(xmlFile),
                Command = new RelayCommand<string>(CreateFromFile!),
                CommandParameter = xmlFile
            };
            // Добавить MenuFlyoutItem в MenuFlyoutSubItem
            Others_CC.Items.Add(fileItem);
            Copy_CC.Items.Add(copyItem);
        }
    }

    private void Load_example()
    {
        // Путь к файлу конфига
        var pathToConfig = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly()
                                                              .Location)!,
                                        @"C:\Program Files (x86)\NoteBook FanControl\Configs\ASUS Vivobook X580VD.xml"); 
        try
        {
            if (File.Exists(pathToConfig))
            {
                ReadEx.Text = File.ReadAllText(pathToConfig);
                ReadEx.Text = ReadEx.Text.Replace("<?xml version=\"1.0\"?>", "\n");
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    private static string GetXmlFileContent(string filePath, TabViewItem newTab) // Получить текст файла
    {
        if (!File.Exists(filePath)) { return "Failed to find file"; }

        try
        {
            return File.ReadAllText(filePath);
        }
        catch
        {
            return File.ReadAllText(@"C:\Program Files (x86)\NoteBook FanControl\Configs\" + newTab.Header + ".xml");
        }
    }

    private async void LoadFanCurvesFromConfig() // Загрузить кривые из конфига
    {
        try
        {
            FanDef.Children.Clear();
            FanDef1.Children.Clear();
            var fanDefGrid = FanDef;
            for (var fdsa = 0; fdsa < 2; fdsa++)
            {
                if (fdsa != 0)
                {
                    fanDefGrid = FanDef1;
                }

                var minimumButton = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Width = 65,
                    Height = 32,
                    Content = new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Min",
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                FontSize = 12,
                                Margin = new Thickness(0, -12, 0, 0),
                                FontWeight = new FontWeight(600)
                            },
                            new TextBlock
                            {
                                Text = "Temp (C)",
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                FontSize = 10,
                                Margin = new Thickness(0, 10, 0, 0)
                            }
                        }
                    }
                };
                var maximumButton = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Width = 65,
                    Height = 32,
                    Content = new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Max",
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                FontSize = 12,
                                Margin = new Thickness(0, -12, 0, 0),
                                FontWeight = new FontWeight(600)
                            },
                            new TextBlock
                            {
                                Text = "Temp (C)",
                                FontSize = 10,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(0, 10, 0, 0)
                            }
                        }
                    }
                };
                var fanspeedButton = new Button
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Width = 65,
                    Height = 32,
                    Content = new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Fan",
                                FontSize = 12,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Center,
                                Margin = new Thickness(0, -12, 0, 0),
                                FontWeight = new FontWeight(600)
                            },
                            new TextBlock
                            {
                                Text = "RPM (%)",
                                FontSize = 10,
                                HorizontalAlignment = HorizontalAlignment.Center,
                                VerticalAlignment = VerticalAlignment.Bottom,
                                Margin = new Thickness(0, 10, 0, 0)
                            }
                        }
                    }
                };
                Grid.SetRow(minimumButton, 0);
                Grid.SetRow(maximumButton, 0);
                Grid.SetRow(fanspeedButton, 0);
                Grid.SetColumn(minimumButton, 0);
                Grid.SetColumn(maximumButton, 2);
                Grid.SetColumn(fanspeedButton, 4);
                fanDefGrid.Children.Add(minimumButton);
                fanDefGrid.Children.Add(maximumButton);
                fanDefGrid.Children.Add(fanspeedButton);
            }

            ExtFan1C.Points.Clear();
            ExtFan2C.Points.Clear();
            var currentFanDef = FanDef;
            // Создаем списки для текущего FanConfiguration 
            var rowCounter = 1; // Счетчик строк в Grid
            try
            {
                var configFilePath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\" +
                                     SettingsService.NbfcConfigXmlName + ".xml";
                Config_Name1.Text = SettingsService.NbfcConfigXmlName;
                if (File.Exists(configFilePath))
                {
                    // Загрузка XML-документа из файла
                    var configXml = XDocument.Load(configFilePath);

                    // Извлечение всех FanConfiguration-элементов в документе
                    var fanConfigurations = configXml.Descendants("FanConfiguration").ToList();

                    // Обход каждого FanConfiguration-элемента в файле
                    for (var i = 0; i < fanConfigurations.Count; i++)
                    {
                        // Извлечение элементов TemperatureThresholds
                        var thresholdElements = fanConfigurations[i].Descendants("TemperatureThreshold");
                        // Обход каждого элемента TemperatureThreshold внутри FanConfiguration
                        foreach (var thresholdElement in thresholdElements)
                        {
                            // Извлечение значений UpThreshold, DownThreshold и FanSpeed из текущего элемента
                            var upThreshold = double.Parse(thresholdElement.Element("UpThreshold")!.Value);
                            var downThreshold = double.Parse(thresholdElement.Element("DownThreshold")!.Value);
                            var fanSpeed = 0.0;
                            try
                            {
                                // Округление значения FanSpeed до ближайшего целого числа
                                fanSpeed = double.Parse(thresholdElement.Element("FanSpeed")!.Value.Trim(),
                                    CultureInfo.InvariantCulture);
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.TraceIt_TraceError(ex);
                            }

                            // Добавление точек на соответствующий Polyline в соответствии с текущими значениями и идентификатором FanConfiguration
                            AddThresholdToPolyline((i + 1).ToString(), downThreshold, upThreshold, fanSpeed);

                            //При каждом Tresholds отрисовывать NumberBox
                            // 1.1 Создаем и настраиваем NumberBox'ы
                            var downThresholdBox = new NumberBox
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Width = 65,
                                Value = double.Parse(thresholdElement.Element("DownThreshold")!.Value),
                                Name = $"DownThresholdBox_{rowCounter - 1}",
                                Margin = rowCounter == 0
                                    ? new Thickness(0, 65, 0, 0)
                                    : new Thickness(0, 5, 0, 0) // Уникальное имя для идентификации NumberBox'а
                            };
                            Grid.SetRow(downThresholdBox, rowCounter);
                            Grid.SetColumn(downThresholdBox, 0);

                            var upThresholdBox = new NumberBox
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Width = 65,
                                Value = double.Parse(thresholdElement.Element("UpThreshold")!.Value),
                                Name = $"UpThresholdBox_{rowCounter - 1}",
                                Margin = rowCounter == 0 ? new Thickness(0, 65, 0, 0) : new Thickness(0, 5, 0, 0)
                            };
                            Grid.SetRow(upThresholdBox, rowCounter);
                            Grid.SetColumn(upThresholdBox, 2);
                            var fanSpeedBox = new NumberBox
                            {
                                HorizontalAlignment = HorizontalAlignment.Left,
                                VerticalAlignment = VerticalAlignment.Top,
                                Width = 65,
                                Value = double.Parse(thresholdElement.Element("FanSpeed")!.Value.Trim(),
                                    CultureInfo.InvariantCulture),
                                Name = $"FanSpeedBox_{rowCounter - 1}",
                                Margin = rowCounter == 0 ? new Thickness(0, 65, 0, 0) : new Thickness(0, 5, 0, 0)
                            };
                            Grid.SetRow(fanSpeedBox, rowCounter);
                            Grid.SetColumn(fanSpeedBox, 4);
                            var fanDefNow = currentFanDef == FanDef ? 1 : 2;
                            // 1.2 Добавляем вызовы методов при изменении значений NumberBox'ов
                            downThresholdBox.ValueChanged += (s, args) => DownThresholdValueChanged(args,
                                int.Parse(s.Name.ToString().Replace("DownThresholdBox_", "")), fanDefNow);
                            upThresholdBox.ValueChanged += (s, args) => UpThresholdValueChanged(args,
                                int.Parse(s.Name.ToString().Replace("UpThresholdBox_", "")), fanDefNow);
                            fanSpeedBox.ValueChanged += (s, args) => FanSpeedValueChanged(args,
                                int.Parse(s.Name.ToString().Replace("FanSpeedBox_", "")), fanDefNow);

                            // 1.3 Добавляем NumberBox'ы в текущий Grid и списки
                            currentFanDef.Children.Add(downThresholdBox);
                            currentFanDef.Children.Add(upThresholdBox);
                            currentFanDef.Children.Add(fanSpeedBox);

                            // Увеличиваем счетчик строк
                            rowCounter++;
                        }

                        // 2. Переключаемся на следующий FanDef, InvFanC и FanConfiguration
                        if (currentFanDef == FanDef)
                        {
                            rowCounter = 1;
                            currentFanDef = FanDef1;
                        }
                        else
                        {
                            break; // Прерываем цикл, так как у нас нет следующих элементов
                        }
                    }

                    if (fanConfigurations.Count == 1)
                    {
                        CurveFan2.Visibility = Visibility.Collapsed;
                        myListButton1.Visibility = Visibility.Collapsed;
                        FanDef1.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        CurveFan2.Visibility = Visibility.Visible;
                        myListButton1.Visibility = Visibility.Visible;
                        FanDef1.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    AdvancedCooler_Curve_Tresholds.Visibility = Visibility.Collapsed;
                    CurveDesc.Text = "AdvancedCooler_CurveDescUnavailable".GetLocalized();

                    await ShowNbfcDialogAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading fan curves from config: " + ex.Message);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }
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

        var nbfcDialog = new ContentDialog
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
            nbfcDialog.XamlRoot = XamlRoot;
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
                nbfcDialog.Content = new TextBlock
                {
                    Text = "Cooler_DownloadNBFC_AfterDesc".GetLocalized(),
                    TextAlignment = TextAlignment.Center
                };
                nbfcDialog.IsPrimaryButtonEnabled = true;
            }
        };
        var result = await nbfcDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            PageService.ReloadPage(typeof(AdvancedКулерViewModel).FullName!); // Вызов метода перезагрузки страницы
        }
    }

    private void AddThresholdToPolyline(string fanName, double minTemp, double maxTemp, double fanSpeed)
    {
        var normalizedX1 = (int)fanSpeed;
        var normalizedY1 = 100 - minTemp;

        var normalizedX2 = (int)fanSpeed;
        var normalizedY2 = 100 - maxTemp;
        if (fanName == "1")
        {
            ExtFan1C.Points.Add(new Point(normalizedX1, (int)normalizedY1));
            ExtFan1C.Points.Add(new Point(normalizedX2, (int)normalizedY2));
        }
        else
        {
            ExtFan2C.Points.Add(new Point(normalizedX1, (int)normalizedY1));
            ExtFan2C.Points.Add(new Point(normalizedX2, (int)normalizedY2));
        }
    }

    #endregion

    #region Event Handlers

    private void NormalMode_Click(object sender, RoutedEventArgs e) // Вернуться в обычный режим управления NBFC
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(КулерViewModel).FullName!);
    }

    private void TabView_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Сохранить текущее положение курсора
        _cursorPosition = e.GetCurrentPoint(MainTab).Position;
    }

    private void Tabs_AddTabButtonClick(TabView sender, object args)
    {
        var contextMenu = (MenuFlyout)Resources["TabContextM"];
        // Отобразить контекстное меню относительно кнопки
        contextMenu.ShowAt(sender, _cursorPosition);
    }

    private void Tabs_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (args.Tab == Example_1Tab || args.Tab == FanC_Tab)
        {
            args.Tab.Visibility = Visibility.Collapsed;
        }
        else
        {
            MainTab.TabItems.Remove(args.Tab);
        }

        // Ищем следующий видимый элемент и устанавливаем его в качестве текущего
        var foundVisibleTab = false;
        for (var i = 0; i < MainTab.TabItems.Count; i++)
        {
            if (MainTab.TabItems[i] is UIElement { Visibility: Visibility.Visible })
            {
                MainTab.SelectedIndex = i;
                foundVisibleTab = true;
                break;
            }
        }

        // Если нет видимых вкладок, создаем новую
        if (!foundVisibleTab)
        {
            // Создаем новую вкладку
            var newTab = new TabViewItem
            {
                Header = "Empty",
                IconSource = new SymbolIconSource { Symbol = Symbol.Placeholder },
                Content = new TextBlock { Text = "There is no any Tabs, open a new one" }
            };
            // Добавляем вкладку в TabView и устанавливаем новую вкладку в качестве текущей
            MainTab.TabItems.Add(newTab);
            MainTab.SelectedItem = newTab;
        }
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e) // Скопировать путь
    {
        var package = new DataPackage();
        package.SetText(ReadEx.Text);
        Clipboard.SetContent(package);
    }

    private async void DownThresholdValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        try
        {
            await ReplaceNumberB("DownThreshold", args.NewValue, values, fan);
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private async void UpThresholdValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        try
        {
            await ReplaceNumberB("UpThreshold", args.NewValue, values, fan);
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private async void FanSpeedValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        try
        {
            await ReplaceNumberB("FanSpeed", args.NewValue, values, fan);
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private void Example_Click(object sender, RoutedEventArgs e)
    {
        Example_1Tab.Visibility = Visibility.Visible;
        MainTab.SelectedItem = Example_1Tab;
    }

    private void Curve_Click(object sender, RoutedEventArgs e)
    {
        FanC_Tab.Visibility = Visibility.Visible;
        MainTab.SelectedItem = FanC_Tab;
    }

    private void Create_Example_Click(object sender, RoutedEventArgs e)
    {
        const string folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";
        const string baseFileName = "ASUS Vivobook X580VD.xml";

        // Находим первое свободное имя файла
        var index = 1;
        string newFileName;
        do
        {
            newFileName = $"Custom{index}.xml";
            index++;
        } while (File.Exists(Path.Combine(folderPath, newFileName)));

        // Создаем новый файл
        var newFilePath = Path.Combine(folderPath, newFileName);
        File.Copy(Path.Combine(folderPath, baseFileName), newFilePath);
        HandleFileItemClick(Path.Combine(folderPath, newFilePath));
        Init_Configs();
    }

    private void Create_Null_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";

        for (var i = 1;; i++)
        {
            var fileName = $"Custom{i}.xml";
            var filePath = Path.Combine(folderPath, fileName);

            if (!File.Exists(filePath))
            {
                // Файл с указанным именем не существует, создаем его
                File.Create(filePath).Close();
                HandleFileItemClick(filePath);
                Init_Configs();
                break;
            }
        }
    }

    private void GridView_ItemClick1(object sender, ItemClickEventArgs e)
    {
        PolilyneChange(ExtFan1C, e);
    }

    private void GridView_ItemClick2(object sender, ItemClickEventArgs e)
    {
        PolilyneChange(ExtFan2C, e);
    }

    private void PolilyneChange(Polyline pln, ItemClickEventArgs e)
    {
        var rect = (Rectangle)e.ClickedItem;
        var color = ((SolidColorBrush)rect.Fill).Color;
        pln.Stroke = new SolidColorBrush(color);
        Task.Delay(10).ContinueWith(_ => myListButton.Flyout.Hide(), TaskScheduler.FromCurrentSynchronizationContext());
        Task.Delay(10).ContinueWith(_ => myListButton1.Flyout.Hide(),
            TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void MyListButton_IsCheckedChanged(ToggleSplitButton sender,
        ToggleSplitButtonIsCheckedChangedEventArgs args)
    {
        ExtFan1C.Visibility = myListButton.IsChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    private void MyListButton1_IsCheckedChanged(ToggleSplitButton sender,
        ToggleSplitButtonIsCheckedChangedEventArgs args)
    {
        ExtFan2C.Visibility = myListButton1.IsChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    #region XML-Config Utils

    private void CreateFromFile(string baseFileName)
    {
        const string folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";

        // Находим первое свободное имя файла
        var index = 1;
        string newFileName;
        do
        {
            newFileName = $"Custom{index}.xml";
            index++;
        } while (File.Exists(Path.Combine(folderPath, newFileName)));

        // Создаем новый файл
        var newFilePath = Path.Combine(folderPath, newFileName);
        File.Copy(Path.Combine(folderPath, baseFileName), newFilePath);
        HandleFileItemClick(Path.Combine(folderPath, newFilePath));
        Init_Configs();
    }

    private void HandleFileItemClick(string filePath)
    {
        // Извлечь имя файла без расширения
        var tabName = Path.GetFileNameWithoutExtension(filePath);

        // Создать новую вкладку
        var newTab = new TabViewItem
        {
            Header = tabName,
            IconSource = new FontIconSource { Glyph = "\uE78C" } // Unicode-код для иконки
        };

        // Создать Grid с кнопкой и RichEditBox
        var tabContent = new Grid();
        var copyButton = new CopyButton
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Content = "\uE8C8"
        };
        AutomationProperties.SetName(copyButton, "Copy link");
        // Создать кнопку для копирования в буфер обмена
        var copyButtonGrid = new Grid
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Margin = new Thickness(0, 3, -2, 0),
            Children =
            {
                new Border
                {
                    CornerRadius = new CornerRadius(4),
                    Width = 40,
                    Height = 40,
                    Shadow = new ThemeShadow(),
                    Translation = new Vector3(0, 0, 20)
                },
                copyButton
            }
        };
        // Добавить обработчик события для кнопки
        copyButton.Click += (_, _) =>
        {
            // Копировать текст в буфер обмена
            var textToCopy = GetXmlFileContent(filePath, newTab);
            var dataPackage = new DataPackage();
            dataPackage.SetText(textToCopy);
            Clipboard.SetContent(dataPackage);
        };

        // Создать RichEditBox и загрузить в него содержимое файла .xml
        var xmlContent = new RichEditBox
        {
            Margin = new Thickness(0, 0, -19, -3),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        xmlContent.Document.SetText(TextSetOptions.None, GetXmlFileContent(filePath, newTab));
        // Создать кнопку для сохранения
        var saveButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Shadow = new ThemeShadow(),
            Translation = new Vector3(0, 0, 20),
            Margin = new Thickness(0, 3, 43, 0),
            Content = new ContentControl
            {
                Content = new FontIcon
                {
                    FontFamily = BackButtonGlyph.FontFamily,
                    Glyph = "\uE74E",
                    Margin = new Thickness(-4, -2, -5, -5)
                }
            }
        };
        saveButton.Click += (_, _) => SaveRichEditBoxContentToFile(xmlContent, filePath, newTab);
        // Создать кнопку для переименования
        var renameButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Margin = new Thickness(0, 3, 89, 0),
            Shadow = new ThemeShadow(),
            Translation = new Vector3(0, 0, 20),
            Content = new ContentControl
            {
                Content = new FontIcon
                {
                    FontFamily = BackButtonGlyph.FontFamily,
                    Glyph = "\uE70F",
                    Margin = new Thickness(-4, -2, -5, -5)
                }
            }
        };
        renameButton.Click += (_, _) =>
        {
            ShowRenameDialog(@"C:\Program Files (x86)\NoteBook FanControl\Configs\" + newTab.Header + ".xml", newTab);
        };
        // Добавить элементы в Grid
        tabContent.Children.Add(xmlContent);
        tabContent.Children.Add(copyButtonGrid);
        tabContent.Children.Add(saveButton);
        tabContent.Children.Add(renameButton);

        // Установить содержимое вкладки
        newTab.Content = tabContent;

        // Добавить вкладку в TabView
        MainTab.TabItems.Add(newTab);

        // Выбрать только что созданную вкладку
        MainTab.SelectedItem = newTab;
    }

    private async void ShowRenameDialog(string filePath, TabViewItem tabItem)
    {
        try
        {
            // Проверяем, что файл имеет в себе имя "Custom"
            var isCustomFile = Path.GetFileNameWithoutExtension(filePath).StartsWith("Custom");

            // Создать ContentDialog
            var renameDialog = new ContentDialog
            {
                Title = isCustomFile
                    ? "AdvancedCooler_Del_Action".GetLocalized()
                    : "AdvancedCooler_Del_Action_Rename".GetLocalized(),
                Content = new TextBox { PlaceholderText = "New_Name".GetLocalized() },
                PrimaryButtonText = "Rename".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            if (isCustomFile)
            {
                renameDialog.SecondaryButtonText = "Delete".GetLocalized();
            }

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                renameDialog.XamlRoot = XamlRoot;
            }

            // Отобразить ContentDialog и обработать результат
            var result = await renameDialog.ShowAsync();
            // Если файл имеет в себе имя "Custom", добавляем опцию "Удалить файл"
            if (result == ContentDialogResult.Secondary)
            {
                await Task.Delay(1000);
                var deleteConfirmationDialog = new ContentDialog
                {
                    Title = "AdvancedCooler_Del_Action_Delete".GetLocalized(),
                    Content = "AdvancedCooler_Del_Action_Sure".GetLocalized(),
                    PrimaryButtonText = "Delete".GetLocalized(),
                    CloseButtonText = "Cancel".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close
                };
                if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                {
                    deleteConfirmationDialog.XamlRoot = XamlRoot;
                }

                var deleteConfirmationResult = await deleteConfirmationDialog.ShowAsync();
                if (deleteConfirmationResult == ContentDialogResult.Primary)
                {
                    // Удалить файл
                    try
                    {
                        File.Delete(filePath);
                        // Удалить вкладку
                        MainTab.TabItems.Remove(tabItem);
                        Init_Configs();
                    }
                    catch
                    {
                        Init_Configs();
                    }
                }
            }


            if (result == ContentDialogResult.Primary)
            {
                var textBox = (TextBox)renameDialog.Content;

                // Проверить, что введено новое имя
                if (!string.IsNullOrWhiteSpace(textBox.Text))
                {
                    var newName = textBox.Text + ".xml";
                    try
                    {
                        // Переименовать файл
                        File.Move(filePath, Path.Combine(Path.GetDirectoryName(filePath)!, newName));

                        // Переименовать вкладку
                        tabItem.Header = textBox.Text;
                        Init_Configs();
                    }
                    catch
                    {
                        Init_Configs();
                    }
                }
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    private static async void SaveRichEditBoxContentToFile(RichEditBox richEditBox, string filePath,
        TabViewItem tabItem)
    {
        try
        {
            // Получить текст из RichEditBox
            var documentRange = richEditBox.Document.GetRange(0, TextConstants.MaxUnitCount);
            documentRange.GetText(TextGetOptions.None, out var content);
            // Сохранить текст в файл
            try
            {
                await FileIO.WriteTextAsync(await StorageFile.GetFileFromPathAsync(filePath), content);
            }
            catch
            {
                await FileIO.WriteTextAsync(
                    await StorageFile.GetFileFromPathAsync(@"C:\Program Files (x86)\NoteBook FanControl\Configs\" +
                                                           tabItem.Header + ".xml"), content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при сохранении файла: {ex.Message}");
        }
    }

    private async Task ReplaceNumberB(string foundValue, double newValue, int unicalId, int fanCount)
    {
        var currentFanDef = FanDef;
        var rowCounter = 0; // Счетчик строк в Grid
        try
        {
            var configFilePath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\" + SettingsService.NbfcConfigXmlName +
                                 ".xml";
            if (File.Exists(configFilePath))
            {
                // Загрузка XML-документа из файла
                var configXml = XDocument.Load(configFilePath);
                // Извлечение всех FanConfiguration-элементов в документе
                var fanConfigurations = configXml.Descendants("FanConfiguration").ToList();
                // Обход каждого FanConfiguration-элемента в файле
                for (var i = 0; i < fanConfigurations.Count; i++)
                {
                    // Извлечение элементов TemperatureThresholds
                    var thresholdElements = fanConfigurations[i].Descendants("TemperatureThreshold");
                    // Обход каждого элемента TemperatureThreshold внутри FanConfiguration
                    foreach (var thresholdElement in thresholdElements)
                    {
                        if (rowCounter == unicalId) // ID Row совпадает с найденным значением
                        {
                            if (currentFanDef == FanDef && fanCount == 1 && i == 0) //Если первый кулер
                            {
                                switch (foundValue)
                                {
                                    case "DownThreshold":
                                        thresholdElement.Element("DownThreshold")!.Value =
                                            newValue.ToString(CultureInfo.InvariantCulture);
                                        break;
                                    case "UpThreshold":
                                        thresholdElement.Element("UpThreshold")!.Value =
                                            newValue.ToString(CultureInfo.InvariantCulture);
                                        break;
                                    case "FanSpeed":
                                        thresholdElement.Element("FanSpeed")!.Value =
                                            newValue.ToString(CultureInfo.InvariantCulture);
                                        break;
                                }

                                try
                                {
                                    configXml.Save(configFilePath);
                                }
                                catch (Exception ex)
                                {
                                    await App.MainWindow.ShowMessageDialogAsync(
                                        "Unable to save XML fan config in target directory: " + ex.Message,
                                        "Critical Error!");
                                }

                                break;
                            }

                            if (currentFanDef == FanDef1 && fanCount == 2 && i != 0) //Если второй кулер (при наличии)
                            {
                                switch (foundValue)
                                {
                                    case "DownThreshold":
                                        thresholdElement.Element("DownThreshold")!.Value = newValue
                                            .ToString(CultureInfo.InvariantCulture).Replace(",", ".");
                                        break;
                                    case "UpThreshold":
                                        thresholdElement.Element("UpThreshold")!.Value = newValue
                                            .ToString(CultureInfo.InvariantCulture).Replace(",", ".");
                                        break;
                                    case "FanSpeed":
                                        thresholdElement.Element("FanSpeed")!.Value = newValue
                                            .ToString(CultureInfo.InvariantCulture).Replace(",", ".");
                                        break;
                                }

                                try
                                {
                                    configXml.Save(configFilePath);
                                }
                                catch (Exception ex)
                                {
                                    await App.MainWindow.ShowMessageDialogAsync(
                                        "Unable to save XML fan config in target directory: " + ex.Message,
                                        "Critical Error!");
                                }
                            }
                        }

                        // Увеличиваем счетчик строк
                        rowCounter++;
                    }

                    // 2. Переключаемся на следующий FanDef, InvFanC и FanConfiguration
                    if (currentFanDef == FanDef)
                    {
                        rowCounter = 0;
                        currentFanDef = FanDef1;
                    }
                    else
                    {
                        break; // Прерываем цикл, так как у нас нет FanDef2 или InvFan3C
                    }
                }
            }
            else
            {
                // Если файл не найден, выводим сообщение
                var messageDialog = new MessageDialog("File not found: " + configFilePath);
                await messageDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(
                "Unable to found or save any values in selected config: " + ex.Message, "Critical Error!");
        }

        LoadFanCurvesFromConfig();
    }

    #endregion
}