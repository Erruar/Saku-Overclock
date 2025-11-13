using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;
using Saku_Overclock.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Text;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;
using Path = System.IO.Path;
using TextConstants = Microsoft.UI.Text.TextConstants;
using TextGetOptions = Microsoft.UI.Text.TextGetOptions;
using TextSetOptions = Microsoft.UI.Text.TextSetOptions;

namespace Saku_Overclock.Views;

public sealed partial class AdvancedКулерPage
{
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();
    private const string folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\"; // Путь к NBFC

    public AdvancedКулерPage()
    {
        App.GetService<AdvancedКулерViewModel>();
        InitializeComponent();
        Loaded += async (_, _) =>
        { 
            await LoadFanCurvesFromConfig();
        };
    }
    
    #region Initialization


    /// <summary>
    /// Получить текст файла
    /// </summary>
    private static string GetXmlFileContent(string filePath, TabViewItem? newTab)
    {
        if (!File.Exists(filePath)) 
        { 
            return "Failed to find file"; 
        }

        try
        {
            return File.ReadAllText(filePath);
        }
        catch
        {
            return File.ReadAllText(folderPath + newTab?.Header + ".xml");
        }
    }

    /// <summary>
    /// Загружает кривые из конфига
    /// </summary>
    private async Task LoadFanCurvesFromConfig()
    {
        try
        {
            FanDef.Children.Clear();
            FanDef1.Children.Clear();
            var fanDefGrid = FanDef;
            for (var fanDefinitionGrid = 0; fanDefinitionGrid < 2; fanDefinitionGrid++)
            {
                if (fanDefinitionGrid != 0)
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
                var fanSpeedButton = new Button
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
                Grid.SetRow(fanSpeedButton, 0);
                Grid.SetColumn(minimumButton, 0);
                Grid.SetColumn(maximumButton, 2);
                Grid.SetColumn(fanSpeedButton, 4);
                fanDefGrid.Children.Add(minimumButton);
                fanDefGrid.Children.Add(maximumButton);
                fanDefGrid.Children.Add(fanSpeedButton);
            }

            ExtFan1C.Points.Clear();
            ExtFan2C.Points.Clear();
            var currentFanDef = FanDef;
            // Создаем списки для текущего FanConfiguration 
            var rowCounter = 1; // Счетчик строк в Grid

            var configFilePath = folderPath +
                                     SettingsService.NbfcConfigXmlName + ".xml";
            CoolerConfigurationName.Text = SettingsService.NbfcConfigXmlName;
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
                            await LogHelper.LogError(ex);
                        }

                        // Добавление точек на соответствующий Polyline в соответствии с текущими значениями и идентификатором FanConfiguration
                        AddPointToCoolerCurve((i + 1).ToString(), downThreshold, upThreshold, fanSpeed);

                        // Для каждого Thresholds создать NumberBox
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
                    Fan1ToggleCurve.Visibility = Visibility.Collapsed;
                    FanDef1.Visibility = Visibility.Collapsed;
                }
                else
                {
                    CurveFan2.Visibility = Visibility.Visible;
                    Fan1ToggleCurve.Visibility = Visibility.Visible;
                    FanDef1.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CurveThresholdsStackPanel.Visibility = Visibility.Collapsed;
                CurveDesc.Text = "AdvancedCooler_CurveDescUnavailable".GetLocalized();

                await ShowNbfcDialogAsync();
            }
        }
        catch (Exception e)
        {
            await LogHelper.LogError("Error loading fan curves from config");
            await LogHelper.LogError(e);
        }
    }
    
    /// <summary>
    /// Показывает диалог загрузки Nbfc
    /// </summary>
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
            CloseButtonText = "CancelThis/Text".GetLocalized(),
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
                    for (var e = 1; e < 5; e++)
                    {
                        if (TryToRunNbfcInstaller(downloadPath))
                        {
                            break;
                        }
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

    /// <summary>
    /// Пытается запустить установщик Nbfc
    /// </summary>
    private static bool TryToRunNbfcInstaller(string downloadPath)
    {
        try
        {
            // Запуск загруженного установочного файла с правами администратора
            Process.Start(new ProcessStartInfo
            {
                FileName = downloadPath,
                Verb = "runas"
            });
        }
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
            return false; // Повторить задачу открытия установщика, в случае если возникла ошибка доступа
        }

        return true;
    }

    /// <summary>
    /// Рисует точки на кривой кулера
    /// </summary>
    private void AddPointToCoolerCurve(string fanName, double minTemp, double maxTemp, double fanSpeed)
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

    /// <summary>
    /// Изменяет значение нижнего порога температуры кривой кулера
    /// </summary>
    private async void DownThresholdValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        try
        {
            await UpdateNbfcFanCurveValues("DownThreshold", args.NewValue, values, fan);
        }
        catch (Exception e)
        {
            await LogHelper.LogError(e);
        }
    }

    /// <summary>
    /// Изменяет значение верхнего порога температуры кривой кулера
    /// </summary>
    private async void UpThresholdValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        try
        {
            await UpdateNbfcFanCurveValues("UpThreshold", args.NewValue, values, fan);
        }
        catch (Exception e)
        {
            await LogHelper.LogError(e);
        }
    }

    /// <summary>
    /// Изменяет значение скорости вращения кулера
    /// </summary>
    private async void FanSpeedValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        try
        {
            await UpdateNbfcFanCurveValues("FanSpeed", args.NewValue, values, fan);
        }
        catch (Exception e)
        {
            await LogHelper.LogError(e);
        }
    }

    /// <summary>
    /// Создаёт конфигурацию управления кулером из примера
    /// </summary>
    private void CreateConfigurationFromExample_Click(object sender, RoutedEventArgs e)
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
        OpenConfigurationFromFile(Path.Combine(folderPath, newFilePath));
    }

    /// <summary>
    /// Создаёт пустую конфигурацию управления кулером
    /// </summary>
    private void Create_Null_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";

        for (var i = 1;; i++)
        {
            var fileName = $"Custom{i}.xml";
            var filePath = Path.Combine(folderPath, fileName);

            if (!File.Exists(filePath)) // Файл с указанным именем не существует, создаем его
            {
                File.Create(filePath).Close();
                OpenConfigurationFromFile(filePath);
                break;
            }
        }
    }

    /// <summary>
    /// Обработчик изменения цвета кривой кулера
    /// </summary>
    private void ChangeCoolerCurveColor_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (((FrameworkElement)sender).Tag.ToString() == "1")
        {
            PolylineChange(ExtFan1C, e);
        }
        else
        {
            PolylineChange(ExtFan2C, e);
        }
    }

    /// <summary>
    /// Изменяет цвет кривой кулера
    /// </summary>
    private void PolylineChange(Polyline polyline, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Rectangle rectangle && rectangle.Fill is SolidColorBrush brush)
        {
            polyline.Stroke = new SolidColorBrush(brush.Color);
            Fan0ToggleCurve.Flyout?.Hide();
            Fan1ToggleCurve.Flyout?.Hide();
        }
    }

    /// <summary>
    /// Изменяет видимость кривой кулера
    /// </summary>
    private void ChangeCoolerCurveVisibility_Checked(ToggleSplitButton sender,
        ToggleSplitButtonIsCheckedChangedEventArgs args)
    {
        if (sender.Tag.ToString() == "1")
        {
            ExtFan1C.Visibility = Fan0ToggleCurve.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            ExtFan2C.Visibility = Fan1ToggleCurve.IsChecked ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    #endregion

    #region XML-Config Utils

    /// <summary>
    /// Создаёт копию файла конфигурации
    /// </summary>
    private void CopyConfigurationFile(string baseFileName)
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
        OpenConfigurationFromFile(Path.Combine(folderPath, newFilePath));
    }

    /// <summary>
    /// Добавляет в список вкладок новую вкладку с редактором кода конфигурации
    /// </summary>
    private void OpenConfigurationFromFile(string filePath)
    {
        // Извлечь имя файла без расширения
        var tabName = Path.GetFileNameWithoutExtension(filePath);

        // Создать новую вкладку
        var newTab = new TabViewItem
        {
            Header = tabName,
            IconSource = new FontIconSource 
            { 
                Glyph = "\uE78C" 
            }
        };

        var defaultCornerRadius = new CornerRadius(12);

        // Создать Grid с кнопкой и RichEditBox
        var tabContent = new Grid() 
        {
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(0, 0, -19, -3),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        var saveBar = new MenuFlyoutItem()
        {
            Text = "Save".GetLocalized(),
            KeyboardAccelerators =
            {
                new KeyboardAccelerator()
                {
                    Modifiers = Windows.System.VirtualKeyModifiers.Control,
                    Key = Windows.System.VirtualKey.S
                }
            }
        };
        var renameBar = new MenuFlyoutItem()
        {
            Text = "Rename".GetLocalized(),
            Tag = folderPath + newTab.Header + ".xml"
        };
        var copyBar = new MenuFlyoutItem()
        {
            Text = "AdvancedCooler_CopyAllAction".GetLocalized(),
            Tag = filePath
        };
        var deleteBar = new MenuFlyoutItem()
        {
            Text = "Delete".GetLocalized(),
            Tag = folderPath + newTab.Header + ".xml"
        };
        var closeBar = new MenuFlyoutItem()
        {
            Text = "AdvancedCooler_CloseFileAction".GetLocalized()
        };

        var menuBar = new MenuBar()
        {
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Height = 35,
            Margin = new Thickness(0, -3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Items =
            {
                new MenuBarItem()
                {
                    Title = "AdvancedCooler_FileOptionsAction".GetLocalized(),
                    Items = 
                    {
                        saveBar,
                        renameBar,
                        deleteBar,
                        closeBar
                    }
                },
                new MenuBarItem()
                {
                    Title = "AdvancedCooler_CopyAction".GetLocalized(),
                    Items =
                    {
                        copyBar
                    }
                },
            }
        };

        // Создать RichEditBox и загрузить в него содержимое файла .xml
        var xmlContent = new RichEditBox
        {
            Margin = new Thickness(0, 35, 0, 0),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            CornerRadius = new CornerRadius(0, 0, 12, 12)
        };
        xmlContent.Document.SetText(TextSetOptions.None, GetXmlFileContent(filePath, newTab));
        
        // Добавить обработчики событий для элементов меню
        renameBar.Click += RenameConfigurationClick;
        deleteBar.Click += DeleteConfigurationClick;
        copyBar.Click += CopyConfigurationClick;
        closeBar.Click += (_, _) =>
        {
            renameBar.Click -= RenameConfigurationClick;
            deleteBar.Click -= DeleteConfigurationClick;
            copyBar.Click -= CopyConfigurationClick;
        };

        // Добавить элементы в Grid
        tabContent.Children.Add(xmlContent);
        tabContent.Children.Add(menuBar);

        // Установить содержимое вкладки
        newTab.Content = tabContent;
    }

    /// <summary>
    /// Копирует текст конфига в буфер обмена
    /// </summary>
    private void CopyConfigurationClick(object sender, RoutedEventArgs e)
    {
        var textToCopy = GetXmlFileContent((sender as FrameworkElement)?.Tag.ToString() ?? "", null);
        var dataPackage = new DataPackage();
        dataPackage.SetText(textToCopy);
        Clipboard.SetContent(dataPackage);
    }

    /// <summary>
    /// Отобразит диалог изменения имени конфига
    /// </summary>
    private async void RenameConfigurationClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = (sender as FrameworkElement)?.Tag.ToString() ?? "";

            var renameDialog = new ContentDialog
            {
                Title = "AdvancedCooler_DeleteAction_Rename".GetLocalized(),
                Content = new TextBox
                {
                    PlaceholderText = "New_Name".GetLocalized()
                },
                PrimaryButtonText = "Rename".GetLocalized(),
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                renameDialog.XamlRoot = XamlRoot;
            }

            var result = await renameDialog.ShowAsync();
            if (result == ContentDialogResult.Primary) // Переименовать файл и вкладку
            {
                var outText = ((TextBox)renameDialog.Content).Text;
                if (!string.IsNullOrWhiteSpace(outText)) // Проверить, корректность нового имени
                {
                    File.Move(filePath, Path.Combine(Path.GetDirectoryName(filePath) ?? @"C:\", outText + ".xml"));
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    /// <summary>
    /// Отобразит диалог удаления конфига
    /// </summary>
    private async void DeleteConfigurationClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var filePath = (sender as FrameworkElement)?.Tag.ToString() ?? "";

            var fileName = Path.GetFileName(filePath);

            var deleteConfirmationDialog = new ContentDialog
            {
                Title = "AdvancedCooler_DeleteAction_Delete".GetLocalized(),
                Content = fileName + " " + "AdvancedCooler_DeleteAction_Sure".GetLocalized(),
                PrimaryButtonText = "Delete".GetLocalized(),
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                deleteConfirmationDialog.XamlRoot = XamlRoot;
            }

            var deleteConfirmationResult = await deleteConfirmationDialog.ShowAsync();
            if (deleteConfirmationResult == ContentDialogResult.Primary) // Удалить файл и вкладку
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            await LogHelper.LogError(ex);
        }
    }

    /// <summary>
    /// Сохраняет изменения кривой кулера
    /// </summary>
    private async Task UpdateNbfcFanCurveValues(string foundValue, double newValue, int uid, int fanCount)
    {
        var currentFanDef = FanDef;
        var rowCounter = 0; // Счетчик строк в Grid
        try
        {
            var configFilePath = folderPath + SettingsService.NbfcConfigXmlName +
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
                        if (rowCounter == uid) // ID Row совпадает с найденным значением
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

            await LoadFanCurvesFromConfig();
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync(
                "Unable to found or save any values in selected config: " + ex.Message, "Critical Error!");
        }
    }

    #endregion
}