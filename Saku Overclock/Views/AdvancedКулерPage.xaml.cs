using System.Globalization;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation.Metadata;
using Windows.Storage;

namespace Saku_Overclock.Views;
public sealed partial class AdvancedКулерPage : Page
{
    public AdvancedКулерViewModel ViewModel
    {
        get;
    }
    private Config config = new();
    private Windows.Foundation.Point cursorPosition; 
    // Создаём списки для хранения NumberBox'ов каждого типа для каждого FanConfiguration
    private readonly List<List<NumberBox>> downThresholdBoxes = new();
    private readonly List<List<NumberBox>> upThresholdBoxes = new();
    private readonly List<List<NumberBox>> fanSpeedBoxes = new();
    public AdvancedКулерPage()
    {
        ViewModel = App.GetService<AdvancedКулерViewModel>();
        InitializeComponent();
        Load_example();
        ConfigLoad();
        config.NBFCFlagConsoleCheckSpeedRunning = false;
        config.FlagRyzenADJConsoleTemperatureCheckRunning = false;
        ConfigSave();
        LoadFanCurvesFromConfig();
        Init_Configs();
    }
    #region JSON and Initialization
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch { }
    }
    public void ConfigLoad()
    {
        try
        {
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 3", "Критическая ошибка!");
        }
    }
    private void Init_Configs()
    {
        // Найти XML конфиги
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\"; 
        // Получить все XML-файлы в этой папке
        var xmlFiles = Directory.GetFiles(folderPath, "*.xml"); 
        Others_CC.Items.Clear(); 
        foreach (var xmlFile in xmlFiles)
        { 
            var fileItem = new MenuFlyoutItem
            {
                Text = System.IO.Path.GetFileName(xmlFile),
                Command = new RelayCommand<string>(HandleFileItemClick!),
                CommandParameter = xmlFile
            };
            var copyItem = new MenuFlyoutItem
            {
                Text = System.IO.Path.GetFileName(xmlFile),
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
        var pathToExecutableFile = System.Reflection.Assembly.GetExecutingAssembly().Location;
        // Получите путь к папке с программой
        var pathToProgramDirectory = System.IO.Path.GetDirectoryName(pathToExecutableFile);
        // Получите путь к файлу конфига
        var pathToConfig = System.IO.Path.Combine(pathToProgramDirectory!, @"C:\Program Files (x86)\NoteBook FanControl\Configs\ASUS Vivobook X580VD.xml");
        try { ReadEx.Text = File.ReadAllText(pathToConfig); ReadEx.Text = ReadEx.Text.Replace("<?xml version=\"1.0\"?>", "\n"); } catch { }
    } 
    private static string GetXmlFileContent(string filePath, TabViewItem newTab)
    { 
        try
        {
            return File.ReadAllText(filePath);
        }
        catch
        {
            return File.ReadAllText(@"C:\Program Files (x86)\NoteBook FanControl\Configs\" + newTab.Header.ToString() + ".xml");
        }
    }
    private async void LoadFanCurvesFromConfig()
    {
        FanDef.Children.Clear();
        FanDef1.Children.Clear();
        ExtFan1C.Points.Clear();
        ExtFan2C.Points.Clear();
        var currentFanDef = FanDef;
        var currentInvFanC = ExtFan1C;
        // Создаем списки для текущего FanConfiguration
        var currentDownThresholdBoxes = new List<NumberBox>();
        var currentUpThresholdBoxes = new List<NumberBox>();
        var currentFanSpeedBoxes = new List<NumberBox>();
        var rowCounter = 0; // Счетчик строк в Grid
        try
        {
            ConfigLoad();
            var configFilePath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\" + config.NBFCConfigXMLName + ".xml";
            Config_Name1.Text = config.NBFCConfigXMLName;
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
                            fanSpeed = double.Parse(thresholdElement.Element("FanSpeed")!.Value.Trim(), CultureInfo.InvariantCulture);
                        }
                        catch { }
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
                            Name = $"DownThresholdBox_{rowCounter}" // Уникальное имя для идентификации NumberBox'а
                        };
                        if (rowCounter == 0) { downThresholdBox.Margin = new Thickness(0, 65, 0, 0); }
                        else { downThresholdBox.Margin = new Thickness(0, 5, 0, 0); }
                        Grid.SetRow(downThresholdBox, rowCounter);
                        Grid.SetColumn(downThresholdBox, 0);

                        var upThresholdBox = new NumberBox
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top,
                            Width = 65,
                            Value = double.Parse(thresholdElement.Element("UpThreshold")!.Value),
                            Name = $"UpThresholdBox_{rowCounter}"
                        };
                        if (rowCounter == 0) { upThresholdBox.Margin = new Thickness(0, 65, 0, 0); }
                        else { upThresholdBox.Margin = new Thickness(0, 5, 0, 0); }
                        Grid.SetRow(upThresholdBox, rowCounter);
                        Grid.SetColumn(upThresholdBox, 2);
                        var fanSpeedBox = new NumberBox
                        {
                            HorizontalAlignment = HorizontalAlignment.Left,
                            VerticalAlignment = VerticalAlignment.Top,
                            Width = 65,
                            Value = double.Parse(thresholdElement.Element("FanSpeed")!.Value.Trim(), CultureInfo.InvariantCulture),
                            Name = $"FanSpeedBox_{rowCounter}"
                        };
                        if (rowCounter == 0) { fanSpeedBox.Margin = new Thickness(0, 65, 0, 0); }
                        else { fanSpeedBox.Margin = new Thickness(0, 5, 0, 0); }
                        Grid.SetRow(fanSpeedBox, rowCounter);
                        Grid.SetColumn(fanSpeedBox, 4);
                        var fanDefNow = 1;
                        if (currentFanDef == FanDef) { fanDefNow = 1; } else { fanDefNow = 2; }
                        // 1.2 Добавляем вызовы методов при изменении значений NumberBox'ов
                        downThresholdBox.ValueChanged += (s, args) => DownThresholdValueChanged(args, int.Parse(s.Name.ToString().Replace("DownThresholdBox_", "")), fanDefNow);
                        upThresholdBox.ValueChanged += (s, args) => UpThresholdValueChanged(args, int.Parse(s.Name.ToString().Replace("UpThresholdBox_", "")), fanDefNow);
                        fanSpeedBox.ValueChanged += (s, args) => FanSpeedValueChanged(args, int.Parse(s.Name.ToString().Replace("FanSpeedBox_", "")), fanDefNow);

                        // 1.3 Добавляем NumberBox'ы в текущий Grid и списки
                        currentFanDef.Children.Add(downThresholdBox);
                        currentFanDef.Children.Add(upThresholdBox);
                        currentFanDef.Children.Add(fanSpeedBox);

                        currentDownThresholdBoxes.Add(downThresholdBox);
                        currentUpThresholdBoxes.Add(upThresholdBox);
                        currentFanSpeedBoxes.Add(fanSpeedBox);
                        // Увеличиваем счетчик строк
                        rowCounter++;
                    }
                    // Добавляем списки NumberBox'ов текущего FanConfiguration в соответствующие списки
                    downThresholdBoxes.Add(currentDownThresholdBoxes);
                    upThresholdBoxes.Add(currentUpThresholdBoxes);
                    fanSpeedBoxes.Add(currentFanSpeedBoxes);

                    // 2. Переключаемся на следующий FanDef, InvFanC и FanConfiguration
                    if (currentFanDef == FanDef)
                    {
                        rowCounter = 0;
                        currentFanDef = FanDef1;
                        currentInvFanC = ExtFan2C;
                    }
                    else
                    {
                        break; // Прерываем цикл, так как у нас нет FanDef2 или InvFan3C
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
                // Если файл не найден, выводим сообщение
                var messageDialog = new Windows.UI.Popups.MessageDialog("File not found: " + configFilePath);
                await messageDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error loading fan curves from config: " + ex.Message);
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
            ExtFan1C.Points.Add(new Windows.Foundation.Point(normalizedX1, (int)normalizedY1));
            ExtFan1C.Points.Add(new Windows.Foundation.Point(normalizedX2, (int)normalizedY2));
        }
        else
        {
            ExtFan2C.Points.Add(new Windows.Foundation.Point(normalizedX1, (int)normalizedY1));
            ExtFan2C.Points.Add(new Windows.Foundation.Point(normalizedX2, (int)normalizedY2));
        }
    }
    #endregion
    #region Event Handlers
    private void NormalMode_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(КулерViewModel).FullName!);
    }
    private void TabView_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        // Сохраните текущее положение курсора
        cursorPosition = e.GetCurrentPoint(MainTab).Position;
    }
    private void Tabs_AddTabButtonClick(TabView sender, object args)
    {
        // Получите кнопку, на которой был выполнен щелчок
        var addButton = (TabView)sender;
        // Получите контекстное меню из ресурсов
        var contextMenu = (MenuFlyout)Resources["TabContextM"];
        // Получите позицию курсора относительно TabView

        // Отобразите контекстное меню относительно кнопки
        contextMenu.ShowAt(addButton, cursorPosition);

        // Остановите дальнейшую обработку события
        //var tab = CreateNewTVI("New Item", "New Item");
        //sender.TabItems.Add(tab);
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
        // Ищем следующий видимый элемент и устанавливаем его в качестве текущего
        for (var i = 0; i < MainTab.TabItems.Count; i++)
        {
            if (MainTab.TabItems[i] is UIElement itemElement && itemElement.Visibility == Visibility.Visible)
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
                IconSource = new Microsoft.UI.Xaml.Controls.SymbolIconSource { Symbol = Symbol.Placeholder },
                Content = new TextBlock { Text = "There is no any Tabs, open a new one" }
            };

            // Добавляем вкладку в TabView
            MainTab.TabItems.Add(newTab);

            // Устанавливаем новую вкладку в качестве текущей
            MainTab.SelectedItem = newTab;
        }
    }
    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        var package = new DataPackage();
        package.SetText(ReadEx.Text);
        Clipboard.SetContent(package);
    }
    private async void DownThresholdValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        await ReplaceNumberB("DownThreshold", args.NewValue, values, fan);
    }
    private async void UpThresholdValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        await ReplaceNumberB("UpThreshold", args.NewValue, values, fan);
    }
    private async void FanSpeedValueChanged(NumberBoxValueChangedEventArgs args, int values, int fan)
    {
        await ReplaceNumberB("FanSpeed", args.NewValue, values, fan);
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
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";
        var baseFileName = "ASUS Vivobook X580VD.xml";

        // Находим первое свободное имя файла
        var index = 1;
        string newFileName;
        do
        {
            newFileName = $"Custom{index}.xml";
            index++;
        } while (File.Exists(System.IO.Path.Combine(folderPath, newFileName)));

        // Создаем новый файл
        var newFilePath = System.IO.Path.Combine(folderPath, newFileName);
        File.Copy(System.IO.Path.Combine(folderPath, baseFileName), newFilePath);
        HandleFileItemClick(System.IO.Path.Combine(folderPath, newFilePath));
        Init_Configs();
    }
    private void Create_Null_Click(object sender, RoutedEventArgs e)
    {
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";

        for (var i = 1; i <= int.MaxValue; i++)
        {
            var fileName = $"Custom{i}.xml";
            var filePath = System.IO.Path.Combine(folderPath, fileName);

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
        Task.Delay(10).ContinueWith(_ => myListButton1.Flyout.Hide(), TaskScheduler.FromCurrentSynchronizationContext());

    }
    private void MyListButton_IsCheckedChanged(ToggleSplitButton sender, ToggleSplitButtonIsCheckedChangedEventArgs args)
    {
        if (myListButton.IsChecked) { ExtFan1C.Visibility = Visibility.Visible; } else { ExtFan1C.Visibility = Visibility.Collapsed; }
    }
    private void MyListButton1_IsCheckedChanged(ToggleSplitButton sender, ToggleSplitButtonIsCheckedChangedEventArgs args)
    {
        if (myListButton1.IsChecked) { ExtFan2C.Visibility = Visibility.Visible; } else { ExtFan2C.Visibility = Visibility.Collapsed; }
    }
    #endregion
    #region XML-Config Utils
    private void CreateFromFile(string baseFileName)
    {
        var folderPath = @"C:\Program Files (x86)\NoteBook FanControl\Configs";

        // Находим первое свободное имя файла
        var index = 1;
        string newFileName;
        do
        {
            newFileName = $"Custom{index}.xml";
            index++;
        } while (File.Exists(System.IO.Path.Combine(folderPath, newFileName)));

        // Создаем новый файл
        var newFilePath = System.IO.Path.Combine(folderPath, newFileName);
        File.Copy(System.IO.Path.Combine(folderPath, baseFileName), newFilePath);
        HandleFileItemClick(System.IO.Path.Combine(folderPath, newFilePath));
        Init_Configs();
    }
    private void HandleFileItemClick(string filePath)
    {
        // Извлечь имя файла без расширения
        var tabName = System.IO.Path.GetFileNameWithoutExtension(filePath);

        // Создать новую вкладку
        var newTab = new TabViewItem
        {
            Header = tabName,
            IconSource = new FontIconSource { Glyph = "\uE78C" } // Unicode-код для иконки
        };

        // Создать Grid с кнопкой и RichEditBox
        var tabContent = new Grid();

        // Создать кнопку для копирования в буфер обмена
        var copyButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Margin = new Thickness(0, 3, 5, 0),
            Content = new ContentControl
            {
                Content = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE8C8",
                    Margin = new Thickness(-4, -2, -5, -5)
                }
            }
        };

        // Добавить обработчик события для кнопки
        copyButton.Click += (sender, args) =>
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
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        xmlContent.Document.SetText(Microsoft.UI.Text.TextSetOptions.None, GetXmlFileContent(filePath, newTab));
        // Создать кнопку для сохранения
        var saveButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Margin = new Thickness(0, 3, 50, 0),
            Content = new ContentControl
            {
                Content = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE74E",
                    Margin = new Thickness(-4, -2, -5, -5)
                }
            }
        };
        saveButton.Click += (sender, e) => SaveRichEditBoxContentToFile(xmlContent, filePath, newTab);
        // Создать кнопку для переименования
        var renameButton = new Button
        {
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right,
            Height = 40,
            Width = 40,
            Margin = new Thickness(0, 3, 96, 0),
            Content = new ContentControl
            {
                Content = new FontIcon
                {
                    FontFamily = new FontFamily("Segoe MDL2 Assets"),
                    Glyph = "\uE70F",
                    Margin = new Thickness(-4, -2, -5, -5)
                }
            }
        };
        renameButton.Click += (sender, e) =>
        {
            ShowRenameDialog(@"C:\Program Files (x86)\NoteBook FanControl\Configs\" + newTab.Header.ToString() + ".xml", newTab);

        };
        // Добавить элементы в Grid
        tabContent.Children.Add(xmlContent);
        tabContent.Children.Add(copyButton);
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
        // Проверяем, что файл имеет в себе имя "Custom"
        var isCustomFile = System.IO.Path.GetFileNameWithoutExtension(filePath).StartsWith("Custom");

        // Создать ContentDialog
        var renameDialog = new ContentDialog
        {
            Title = isCustomFile ? "AdvancedCooler_Del_Action".GetLocalized() : "AdvancedCooler_Del_Action_Rename".GetLocalized(),
            Content = new TextBox { PlaceholderText = "New_Name".GetLocalized() },
            PrimaryButtonText = "Rename".GetLocalized(),
            CloseButtonText = "Cancel".GetLocalized(),
            DefaultButton = ContentDialogButton.Close
        };
        if (isCustomFile) { renameDialog.SecondaryButtonText = "Delete".GetLocalized(); }
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
                    File.Move(filePath, System.IO.Path.Combine(System.IO.Path.GetDirectoryName(filePath)!, newName));

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
    private static async void SaveRichEditBoxContentToFile(RichEditBox richEditBox, string filePath, TabViewItem tabItem)
    {
        try
        {
            // Получить текст из RichEditBox
            var documentRange = richEditBox.Document.GetRange(0, Microsoft.UI.Text.TextConstants.MaxUnitCount);
            var content = "";
            documentRange.GetText(Microsoft.UI.Text.TextGetOptions.None, out content);
            // Сохранить текст в файл
            try
            {
                await FileIO.WriteTextAsync(await StorageFile.GetFileFromPathAsync(filePath), content);
            }
            catch
            {
                await FileIO.WriteTextAsync(await StorageFile.GetFileFromPathAsync(@"C:\Program Files (x86)\NoteBook FanControl\Configs\" + tabItem.Header.ToString() + ".xml"), content);
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
            ConfigLoad();
            var configFilePath = @"C:\Program Files (x86)\NoteBook FanControl\Configs\" + config.NBFCConfigXMLName + ".xml";
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
                                        thresholdElement.Element("DownThreshold")!.Value = newValue.ToString();
                                        break;
                                    case "UpThreshold":
                                        thresholdElement.Element("UpThreshold")!.Value = newValue.ToString();
                                        break;
                                    case "FanSpeed":
                                        thresholdElement.Element("FanSpeed")!.Value = newValue.ToString();
                                        break;
                                }
                                try
                                {
                                    configXml.Save(configFilePath);
                                }
                                catch (Exception ex)
                                {
                                    await App.MainWindow.ShowMessageDialogAsync("Unable to save XML fan config in target directory: " + ex.Message, "Critical Error!");
                                }
                                break;
                            }
                            if (currentFanDef == FanDef1 && fanCount == 2 && i != 0)//Если второй кулер (при наличии)
                            {
                                switch (foundValue)
                                {
                                    case "DownThreshold":
                                        thresholdElement.Element("DownThreshold")!.Value = newValue.ToString().Replace(",", ".");
                                        break;
                                    case "UpThreshold":
                                        thresholdElement.Element("UpThreshold")!.Value = newValue.ToString().Replace(",", ".");
                                        break;
                                    case "FanSpeed":
                                        thresholdElement.Element("FanSpeed")!.Value = newValue.ToString().Replace(",",".");
                                        break;
                                }
                                try
                                {
                                    configXml.Save(configFilePath);
                                }
                                catch (Exception ex)
                                {
                                    await App.MainWindow.ShowMessageDialogAsync("Unable to save XML fan config in target directory: " + ex.Message, "Critical Error!");
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
                var messageDialog = new Windows.UI.Popups.MessageDialog("File not found: " + configFilePath);
                await messageDialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync("Unable to found or save any values in selected config: " + ex.Message,"Critical Error!");
        }
        LoadFanCurvesFromConfig();
    }
    #endregion
}