using System.Drawing;
using System.Drawing.Drawing2D;
using System.Management;
using System.Text.RegularExpressions;
using Accord.Math;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using ZenStates.Core;

namespace Saku_Overclock.Views;
public sealed partial class ИнформацияPage : Page
{
    private Config config = new();
    private JsonContainers.RTSSsettings rtssset = new();
    private JsonContainers.NiIconsSettings niicons = new();
    private readonly Dictionary<string, System.Windows.Forms.NotifyIcon> trayIcons = [];
    private class MinMax
    {
        public float Min;
        public float Max;
    }
    private readonly List<MinMax> niicons_Min_MaxValues = [new(), new(), new(), new(), new(), new(), new(), new(), new(), new(), new()];
    public double refreshtime;
    private bool loaded = false;
    private string? rtss_line;
    private readonly List<InfoPageCPUPoints> CPUPointer = [];
    private readonly List<InfoPageCPUPoints> GPUPointer = [];
    private readonly List<InfoPageCPUPoints> RAMPointer = [];
    private readonly List<InfoPageCPUPoints> VRMPointer = [];
    private readonly List<InfoPageCPUPoints> BATPointer = [];
    private readonly List<InfoPageCPUPoints> PSTPointer = [];
    private readonly List<double> PSTatesList = [0, 0, 0];
    private double MaxGFXClock = 0.1;
    private decimal MaxBatRate = 0.1m;
    private Microsoft.UI.Xaml.Media.Brush? TransparentBrush;
    private Microsoft.UI.Xaml.Media.Brush? SelectedBrush;
    private Microsoft.UI.Xaml.Media.Brush? SelectedBorderBrush;
    private int SelectedGroup = 0;
    private bool IsAppInTray = false;
    private IntPtr ryzenAccess;
    private string CPUName = "Unknown";
    private string GPUName = "Unknown";
    private string RAMName = "Unknown";
    private string? BATName = "Unknown";
    private int numberOfCores = 0;
    private int numberOfLogicalProcessors = 0;
    private System.Windows.Threading.DispatcherTimer? dispatcherTimer;
    private readonly Cpu? cpu;
    public ИнформацияViewModel ViewModel
    {
        get;
    }
    public ИнформацияPage()
    {
        ViewModel = App.GetService<ИнформацияViewModel>();
        InitializeComponent();
        try
        {
            cpu ??= CpuSingleton.GetInstance();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
        RtssLoad();
        Loaded += (s, a) =>
        {
            loaded = true;
            SelectedBrush = CPUBannerButton.Background;
            SelectedBorderBrush = CPUBannerButton.BorderBrush;
            TransparentBrush = GPUBannerButton.Background;
            GetCPUInfo();
            GetRAMInfo();
            ReadPstate();
            GetBATInfo();
            if (CPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow ??= new ThemeShadow();
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }
            try
            {
                ConfigLoad();
                infoRTSSButton.IsChecked = config.RTSSMetricsEnabled;
                infoNiIconsButton.IsChecked = config.NiIconsEnabled;
                if (config.NiIconsEnabled)
                {
                    CreateNotifyIcons();
                }
            }
            catch
            {

            }
        };
        Unloaded += ИнформацияPage_Unloaded;
    }

    #region JSON and Initialization
    //JSON форматирование
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
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"))!;
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 3", "Критическая ошибка!");
        }
    }
    public void RtssSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json", JsonConvert.SerializeObject(rtssset, Formatting.Indented));
        }
        catch { }
    }
    public void RtssLoad()
    {
        try
        {
            rtssset = JsonConvert.DeserializeObject<JsonContainers.RTSSsettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\rtssparam.json"))!;
            rtssset.RTSS_Elements.RemoveRange(0, 9);
            //if (rtssset == null) { rtssset = new JsonContainers.RTSSsettings(); RtssSave(); }
        }
        catch { rtssset = new JsonContainers.RTSSsettings(); RtssSave(); }
    }
    public void NiSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json", JsonConvert.SerializeObject(niicons, Formatting.Indented));
        }
        catch { }
    }
    public void NiLoad()
    {
        try
        {
            niicons = JsonConvert.DeserializeObject<JsonContainers.NiIconsSettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\niicons.json"))!;
        }
        catch { niicons = new JsonContainers.NiIconsSettings(); NiSave(); }
    }

    public void DisposeAllNotifyIcons()
    {
        // Перебираем все иконки и вызываем Dispose для каждой из них
        foreach (var icon in trayIcons.Values)
        {
            icon.Dispose();
        }

        // Очищаем коллекцию иконок
        trayIcons.Clear();
    }
    public void CreateNotifyIcons()
    {
        DisposeAllNotifyIcons(); // Уничтожаем старые иконки перед созданием новых

        NiLoad();
        // Если нет элементов, не создаём иконки
        if (niicons.Elements == null || niicons.Elements.Count == 0)
        {
            return;
        }

        foreach (var element in niicons.Elements)
        {
            if (!element.IsEnabled)
            {
                continue;
            }

            // Создаём NotifyIcon
            var notifyIcon = new System.Windows.Forms.NotifyIcon
            {
                Visible = true,

                // Генерация иконки
                Icon = CreateIconFromElement(element)!
            };
            if (element.ContextMenuType != 0)
            {
                notifyIcon.Text = element.Name;
            }
            trayIcons[element.Name] = notifyIcon;
        }
    }

    private static System.Drawing.Icon? CreateIconFromElement(JsonContainers.NiIconsElements element)
    {
        // Создаём Grid виртуально и растрируем в Bitmap
        // Пример создания иконки будет зависеть от элемента:
        // 1. Создание формы (круг, квадрат, логотип и т.д.)
        // 2. Заливка цвета с заданной прозрачностью
        // 3. Наложение текста с указанным размером шрифта

        // Для простоты примера создадим пустую иконку
        var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            // Задаём цвет фона и форму
            var bgColor = System.Drawing.ColorTranslator.FromHtml("#" + element.Color);
            System.Drawing.Brush bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb((int)(element.BgOpacity * 255), bgColor));
            switch (element.IconShape)
            {
                case 0: // Куб
                    g.FillRectangle(bgBrush, 0, 0, 32, 32);
                    break;
                case 1: // Скруглённый куб
                    var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                    g.FillPath(bgBrush, path!);
                    break;
                case 2: // Круг
                    g.FillEllipse(bgBrush, 0, 0, 32, 32);
                    break;
                // Добавьте остальные фигуры и обработку ico
                default:
                    g.FillRectangle(bgBrush, 0, 0, 32, 32);
                    break;
            }

            // Добавляем текст
            try
            {
                var font = new System.Drawing.Font(new System.Drawing.FontFamily("Arial"), element.FontSize * 2, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
                System.Windows.Forms.TextRenderer.DrawText(g, "Text", font, new System.Drawing.Point(-6, 5), InvertColor(element.Color)); 

            }
            catch { } // Игнорим
        }
        try
        {
            return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
        }
        catch
        { 
            return null;
        }
    }
    private static GraphicsPath? CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        // Проверка корректности значений
        if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
        {
            return null;
        }

        try
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;
            var size = new Size(diameter, diameter);
            var arc = new Rectangle(rect.Location, size);

            // Верхний левый угол
            path.AddArc(arc, 180, 90);

            // Верхний правый угол
            arc.X = rect.Right - diameter;
            path.AddArc(arc, 270, 90);

            // Нижний правый угол
            arc.Y = rect.Bottom - diameter;
            path.AddArc(arc, 0, 90);

            // Нижний левый угол
            arc.X = rect.Left;
            path.AddArc(arc, 90, 90);

            path.CloseFigure();
            return path;
        }
        catch
        {
            return null;
        }
    }
    public void Change_Ni_Icons_Text(string IconName, string? NewText, string? TooltipText = null, string? AdvancedTooltip = null)
    {
        try
        {
            if (trayIcons.TryGetValue(IconName, out var notifyIcon))
            {
                foreach (var element in niicons.Elements)
                {
                    if (element.Name == IconName)
                    {
                        // Изменяем текст на иконке (слой 2)
                        notifyIcon.Icon = UpdateIconText(NewText, element.Color, element.FontSize, element.IconShape, element.BgOpacity, notifyIcon.Icon);

                        // Обновляем TooltipText, если он задан
                        if (TooltipText != null && notifyIcon.Text != null)
                        {
                            notifyIcon.Text = element.ContextMenuType == 2 ? TooltipText + "\n" + AdvancedTooltip : TooltipText;
                        }
                    }
                }
            }
        }
        catch
        {
            CreateNotifyIcons();
        }
    }

    private static System.Drawing.Icon UpdateIconText(string ? newText, string NewColor, int FontSize, int IconShape, double Opacity, System.Drawing.Icon? oldIcon = null)
    {
        // Уничтожаем старую иконку, если она существует
        if (oldIcon != null)
        {
            DestroyIcon(oldIcon.Handle); // Освобождение старой иконки
            oldIcon.Dispose(); // Освобождаем ресурсы иконки
        }
        // Создаём новую иконку на основе существующей с новым текстом
        var bitmap = new System.Drawing.Bitmap(32, 32);
        var g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Цвет фона и кисть
        var bgColor = System.Drawing.ColorTranslator.FromHtml("#" + NewColor);
        var bgBrush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb((int)(Opacity * 255), bgColor));
        // Рисуем фон иконки в зависимости от формы
        switch (IconShape)
        {
            case 0: // Куб
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
            case 1: // Скруглённый куб
                var path = CreateRoundedRectanglePath(new Rectangle(0, 0, 32, 32), 7);
                if (path != null)
                {
                    g.FillPath(bgBrush, path);
                }
                else
                {
                    g.FillRectangle(bgBrush, 0, 0, 32, 32);
                }
                break;
            case 2: // Круг
                g.FillEllipse(bgBrush, 0, 0, 32, 32);
                break;
            // Добавьте остальные фигуры и обработку ico при необходимости
            default:
                g.FillRectangle(bgBrush, 0, 0, 32, 32);
                break;
        }
        // Определение позиции текста
        var textPosition = GetTextPosition(newText, FontSize, out var NewFontSize);
        // Установка шрифта
        var font = new System.Drawing.Font(new System.Drawing.FontFamily("Arial"), NewFontSize * 2, System.Drawing.FontStyle.Regular, GraphicsUnit.Pixel);

        // Рисуем текст
        System.Windows.Forms.TextRenderer.DrawText(g, newText, font, textPosition, InvertColor(NewColor));
        // Создание иконки из Bitmap
        // Создание иконки из Bitmap и освобождение ресурсов
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon()); 
    }
    // Метод для освобождения ресурсов, используемый после GetHicon()
    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
    // Метод для вычисления позиции текста в зависимости от условий
    private static Point GetTextPosition(string? newText, int fontSize, out int NewFontSize)
    {
        // По умолчанию позиция текста для трех символов
        var position = new Point(-5, 6);

        // Определение масштаба шрифта
        var scale = fontSize / 9.0f;

        if (newText != null)
        {
            // Если текст состоит из одного символа
            if (newText.Contains(",") && newText.Split(',')[0].Length == 1 && newText.Split(',')[1].Length >= 2)
            {
                position = new Point(-5, 6);
            }
            if (newText.Contains(",") && newText.Split(',')[0].Length == 2 || newText.Contains(",") && newText.Split(',')[0].Length == 1 && newText.Split(',')[1].Length <= 1)
            {
                position = new Point(2, 6);
            }
            // Если текст состоит из четырёх символов
            else if (newText.Contains(",") && newText.Split(',')[0].Length == 4)
            {
                position = new Point(-6, 8);
                fontSize -= 2; // уменьшение размера шрифта на 2
            }
        }

        // Корректируем позицию текста на основе масштаба
        position.X = (int)Math.Floor(position.X * scale);
        position.Y = (int)Math.Floor(position.Y * scale);
        NewFontSize = fontSize;
        return position;
    }
    private static System.Drawing.Color InvertColor(string Color)
    {
        var r = 0;
        var g = 0;
        var b = 0;
        if (!string.IsNullOrEmpty(Color))
        {
            // Убираем символ #, если он присутствует
            var valuestring = Color.TrimStart('#');
            // Парсим цветовые компоненты
            r = System.Convert.ToInt32(valuestring!.Substring(0, 2), 16);
            g = System.Convert.ToInt32(valuestring!.Substring(2, 2), 16);
            b = System.Convert.ToInt32(valuestring!.Substring(4, 2), 16);
        }
        r = 255 - r;
        g = 255 - g;
        b = 255 - b;
        return System.Drawing.Color.FromArgb(r, g, b);
    }

    public static int GetCPUCores()
    {
        var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
        try
        {
            foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
            {
                var numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                var numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                var l2Size = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;

                return numberOfLogicalProcessors == numberOfCores
                    ? numberOfCores
                    : int.Parse(GetSystemInfo.GetBigLITTLE(numberOfCores, l2Size));
            }
        }
        catch
        {
            return 0;
        }
        return 0;
    }
    private async void GetCPUInfo()
    {
        try
        {
            // CPU information using WMI
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");

            var name = "";
            var description = "";
            double l2Size = 0;
            double l3Size = 0;
            var baseClock = "";

            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    name = queryObj["Name"].ToString();
                    description = queryObj["Description"].ToString();
                    numberOfCores = Convert.ToInt32(queryObj["NumberOfCores"]);
                    numberOfLogicalProcessors = Convert.ToInt32(queryObj["NumberOfLogicalProcessors"]);
                    l2Size = Convert.ToDouble(queryObj["L2CacheSize"]) / 1024;
                    l3Size = Convert.ToDouble(queryObj["L3CacheSize"]) / 1024;
                    baseClock = queryObj["MaxClockSpeed"].ToString();
                }
                try
                {
                    if (GetSystemInfo.GetGPUName(0) != null)
                    {
                        GPUName = GetSystemInfo.GetGPUName(0)!.Contains("AMD") ? GetSystemInfo.GetGPUName(0)! : GetSystemInfo.GetGPUName(1)!;
                    }
                }
                catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
            });
            InfoCPUSectionGridBuilder();
            tbProcessor.Text = name;
            CPUName = name;
            tbCaption.Text = description;
            var codeName = GetSystemInfo.Codename();
            //CODENAME OVERRIDE
            // codeName = "Renoir";
            if (codeName != "")
            {
                tbCodename.Text = codeName;
                tbCodename1.Visibility = Visibility.Collapsed;
                tbCode1.Visibility = Visibility.Collapsed;
            }
            else
            {
                try
                {
                    tbCodename1.Text = $"{cpu?.info.codeName}";
                }
                catch
                {
                    tbCodename1.Visibility = Visibility.Collapsed;
                    tbCode1.Visibility = Visibility.Collapsed;
                }
                tbCodename.Visibility = Visibility.Collapsed;
                tbCode.Visibility = Visibility.Collapsed;
            }
            try
            {
                tbSMU.Text = cpu?.systemInfo.GetSmuVersionString();
            }
            catch
            {
                tbSMU.Visibility = Visibility.Collapsed;
                infoSMU.Visibility = Visibility.Collapsed;
            }
            tbCores.Text = numberOfLogicalProcessors == numberOfCores ? numberOfCores.ToString() : GetSystemInfo.GetBigLITTLE(numberOfCores, l2Size);
            tbThreads.Text = numberOfLogicalProcessors.ToString();
            tbL3Cache.Text = $"{l3Size:0.##} MB";
            uint sum = 0;
            foreach (var number in GetSystemInfo.GetCacheSizes(GetSystemInfo.CacheLevel.Level1))
            {
                sum += number;
            }
            decimal total = sum;
            total /= 1024;
            tbL1Cache.Text = $"{total:0.##} MB";
            sum = 0;
            foreach (var number in GetSystemInfo.GetCacheSizes(GetSystemInfo.CacheLevel.Level2))
            {
                sum += number;
            }
            total = sum;
            total /= 1024;
            tbL2Cache.Text = $"{total:0.##} MB";
            tbBaseClock.Text = $"{baseClock} MHz";
            tbInstructions.Text = GetSystemInfo.InstructionSets();
        }
        catch (ManagementException ex)
        {
            Console.WriteLine("An error occurred while querying for WMI data: " + ex.Message);
        }
    }
    private void InfoCPUSectionGridBuilder()
    {
        InfoMainCPUFreqGrid.RowDefinitions.Clear();
        InfoMainCPUFreqGrid.ColumnDefinitions.Clear();
        /*numberOfCores = 8;
        numberOfLogicalProcessors = 16;*/
        var backupNumberLogical = numberOfLogicalProcessors;
        if (numberOfCores > 2)
        {
            numberOfLogicalProcessors = numberOfCores;
        }
        for (var i = 0; i < numberOfLogicalProcessors / 2; i++)
        {
            InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
            InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        if (numberOfLogicalProcessors % 2 != 0 || numberOfLogicalProcessors == 2)
        {
            InfoMainCPUFreqGrid.RowDefinitions.Add(new RowDefinition());
            InfoMainCPUFreqGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }
        numberOfLogicalProcessors = backupNumberLogical;
        var coreCounter = (SelectedGroup == 0 || SelectedGroup == 5) ? /*Это секция процессор или PStates*/
            (numberOfCores > 2 ? numberOfCores : /*Это секция процессор или PStates - да! Количество ядер больше 2? - да! тогда coreCounter - количество ядер numberOfCores*/
            (infoCPUSectionComboBox.SelectedIndex == 0 ? numberOfLogicalProcessors /*Нет! У процессора менее или ровно 2 ядра, Выбрано отображение частоты? - да! - тогда numberOfLogicalProcessors*/
            : numberOfCores)) /*Выбрана не частота, хотя при этом у нас меньше или ровно 2 ядра и это секция 0 или 5, тогда - numberOfCores*/
            : SelectedGroup == 1 ? /*Это НЕ секция процессор или PStates. Это секция GFX?*/
            new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>().Count() /*Да! - Это секция GFX - тогда найти количество видеокарт*/
            : (SelectedGroup == 2 ? /*Нет! Выбрана не секция 0, 1, 5, возможно что-то другое? Выбрана секция 2?*/
            tbRAMModel.Text.Split('/').Length /*Да! Выбрана секция RAM, найти количество установленных плат ОЗУ*/
            : (SelectedGroup == 3 ? 4 /*Это не секции 0, 1, 2, 5! Это секция 3? - да! Тогда - 4*/
            : 1)); /*Это не секции 0, 1, 2, 3, 5! Тогда - 1*/
        for (var j = 0; j < InfoMainCPUFreqGrid.RowDefinitions.Count; j++)
        {
            for (var f = 0; f < InfoMainCPUFreqGrid.ColumnDefinitions.Count; f++)
            {
                if (coreCounter <= 0)
                {
                    return;
                }
                var currCore = (SelectedGroup == 0 || SelectedGroup == 5) ?
                    (numberOfCores > 2 ?
                    numberOfCores - coreCounter
                    : infoCPUSectionComboBox.SelectedIndex == 0 ?
                    numberOfLogicalProcessors - coreCounter
                    : numberOfCores - coreCounter)
                    : SelectedGroup == 1 ?
                    new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>().Count() - coreCounter
                    : (SelectedGroup == 2 ?
                    tbRAMModel.Text.Split('/').Length - coreCounter
                    : (SelectedGroup == 3 ?
                    4 - coreCounter
                    : 0));
                var elementButton = new Grid()
                {
                    VerticalAlignment = VerticalAlignment.Stretch,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(3, 3, 3, 3),
                    Children =
                        {

                            new Button()
                            {
                                Shadow = new ThemeShadow(),
                                Translation = new System.Numerics.Vector3(0,0,20),
                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                VerticalAlignment = VerticalAlignment.Stretch,
                                Content = new Grid()
                                {
                                    VerticalAlignment = VerticalAlignment.Stretch,
                                    HorizontalAlignment = HorizontalAlignment.Stretch,
                                    Children =
                                    {
                                        new TextBlock
                                        {
                                            VerticalAlignment = VerticalAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Left,
                                            Text = currCore.ToString(),
                                            FontWeight = new Windows.UI.Text.FontWeight(200)
                                        },
                                        new TextBlock
                                        {
                                            Text = "0.00 Ghz",
                                            Name = $"FreqButtonText_{currCore}",
                                            VerticalAlignment = VerticalAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Center,
                                            FontWeight = new Windows.UI.Text.FontWeight(800)
                                        },
                                        new TextBlock
                                        {
                                            VerticalAlignment = VerticalAlignment.Center,
                                            HorizontalAlignment = HorizontalAlignment.Right,
                                            Text = (SelectedGroup == 0 || SelectedGroup == 5) ?
                                            (currCore < numberOfCores ? "InfoCPUCore".GetLocalized()
                                            : "InfoCPUThread".GetLocalized())
                                            : (SelectedGroup == 1 ? "InfoGPUName".GetLocalized()
                                            : (SelectedGroup == 2 ? tbSlots.Text.Split('*')[1].Replace("Bit","")
                                            : (SelectedGroup == 3 ?
                                              (currCore == 0 ? "VRM EDC"
                                            : ( currCore == 1 ? "VRM TDC"
                                            : (currCore == 2 ? "SoC EDC"
                                            : "SoC TDC")))
                                            : "InfoBatteryName".GetLocalized()))),
                                            FontWeight = new Windows.UI.Text.FontWeight(200)
                                        }
                                    }
                                }

                            }
                        }
                };

                Grid.SetRow(elementButton, j); Grid.SetColumn(elementButton, f);
                InfoMainCPUFreqGrid.Children.Add(elementButton);
                coreCounter--;
            }
        }
    }
    private void GetBATInfo()
    {
        try
        {
            tbBAT.Text = GetSystemInfo.GetBatteryPercent().ToString() + "W";
            tbBATState.Text = GetSystemInfo.GetBatteryStatus().ToString();
            tbBATHealth.Text = $"{100 - (GetSystemInfo.GetBatteryHealth() * 100):0.##}%";
            tbBATCycles.Text = $"{GetSystemInfo.GetBatteryCycle()}";
            tbBATCapacity.Text = $"{GetSystemInfo.ReadFullChargeCapacity()}mAh/{GetSystemInfo.ReadDesignCapacity()}mAh";
            tbBATChargeRate.Text = $"{(GetSystemInfo.GetBatteryRate() / 1000):0.##}W";
            BATName = GetSystemInfo.GetBatteryName();
        }
        catch
        {
            if (BATBannerButton.Visibility != Visibility.Collapsed) { BATBannerButton.Visibility = Visibility.Collapsed; }
        }
    }
    private async void GetRAMInfo()
    {
        double capacity = 0;
        var speed = 0;
        var type = 0;
        var width = 0;
        var slots = 0;
        var producer = "";
        var model = "";

        try
        {
            var searcher = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_PhysicalMemory");
            await Task.Run(() =>
            {
                foreach (var queryObj in searcher.Get().Cast<ManagementObject>())
                {
                    if (producer == "") { producer = queryObj["Manufacturer"].ToString(); }
                    else if (!producer!.Contains(value: queryObj["Manufacturer"].ToString()!)) { producer = $"{producer}/{queryObj["Manufacturer"]}"; }
                    if (model == "") { model = queryObj["PartNumber"].ToString(); }
                    else if (!model!.Contains(value: queryObj["PartNumber"].ToString()!))
                    {
                        model = $"{model}/{queryObj["PartNumber"]}";
                    }
                    capacity += Convert.ToDouble(queryObj["Capacity"]);
                    speed = Convert.ToInt32(queryObj["ConfiguredClockSpeed"]);
                    type = Convert.ToInt32(queryObj["SMBIOSMemoryType"]);
                    width += Convert.ToInt32(queryObj["DataWidth"]);
                    slots++;
                }
            });
            capacity = capacity / 1024 / 1024 / 1024;
            var DDRType = "";
            DDRType = type switch
            {
                20 => "DDR",
                21 => "DDR2",
                24 => "DDR3",
                26 => "DDR4",
                30 => "LPDDR4",
                34 => "DDR5",
                35 => "LPDDR5",
                36 => "LPDDR5X",
                _ => $"Unknown ({type})",
            };
            RAMName = $"{capacity} GB {DDRType} @ {speed} MT/s";
            tbRAM.Text = speed + "MT/s";
            tbRAMProducer.Text = producer;
            tbRAMModel.Text = model.Replace(" ", null);
            tbWidth.Text = $"{width} bit";
            tbSlots.Text = $"{slots} * {width / slots} bit";
            tbTCL.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 0, 6) + "T";
            tbTRCDWR.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 24, 6) + "T";
            tbTRCDRD.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 16, 6) + "T";
            tbTRAS.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50204), 8, 7) + "T";
            tbTRP.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50208), 16, 6) + "T";
            tbTRC.Text = Utils.GetBits(cpu!.ReadDword(0 | 0x50208), 0, 8) + "T";
        }
        catch (Exception ex)
        {
            SendSMUCommand.TraceIt_TraceError(ex.ToString());
        }
    }
    public static void CalculatePstateDetails(uint eax, ref uint IddDiv, ref uint IddVal, ref uint CpuVid, ref uint CpuDfsId, ref uint CpuFid)
    {
        IddDiv = eax >> 30;
        IddVal = eax >> 22 & 0xFF;
        CpuVid = eax >> 14 & 0xFF;
        CpuDfsId = eax >> 8 & 0x3F;
        CpuFid = eax & 0xFF;
    }
    private void ReadPstate()
    {
        try
        {
            for (var i = 0; i < 3; i++)
            {
                uint eax = default, edx = default;
                var pstateId = i;
                try
                {
                    if (cpu?.ReadMsr(Convert.ToUInt32(Convert.ToInt64(0xC0010064) + pstateId), ref eax, ref edx) == false)
                    {
                        App.MainWindow.ShowMessageDialogAsync("Error while reading CPU Pstate", "Critical Error");
                        return;
                    }
                }
                catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
                uint IddDiv = 0x0;
                uint IddVal = 0x0;
                uint CpuVid = 0x0;
                uint CpuDfsId = 0x0;
                uint CpuFid = 0x0;
                CalculatePstateDetails(eax, ref IddDiv, ref IddVal, ref CpuVid, ref CpuDfsId, ref CpuFid);
                var textBlock = (TextBlock)InfoPSTSectionMetrics.FindName($"tbPSTP{i}");
                textBlock.Text = $"FID: {Convert.ToString(CpuFid, 10)}/DID: {Convert.ToString(CpuDfsId, 10)}\n{CpuFid * 25 / (CpuDfsId * 12.5) / 10}" + "infoAGHZ".GetLocalized();
                PSTatesList[i] = CpuFid * 25 / (CpuDfsId * 12.5) / 10;
            }
        }
        catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
    }
    private void Window_VisibilityChanged(object sender, WindowVisibilityChangedEventArgs args)
    {
        if (args.Visible)
        {
            dispatcherTimer?.Start();
            IsAppInTray = false;
        }
        else
        {
            if (infoRTSSButton.IsChecked == false && config.NiIconsEnabled == false)
            {
                dispatcherTimer?.Stop();
                IsAppInTray = true;
            }
        }
    }
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e); StartInfoUpdate();
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e); StopInfoUpdate();
    }
    private void UpdateInfoAsync()
    {
        try
        {
            if (!loaded) { return; }

            if (!IsAppInTray)
            {
                if (SelectedGroup != 0)
                {
                    infoCPUSectionComboBox.Visibility = Visibility.Collapsed;
                    InfoCPUComboBoxBorderSharedShadow_Element.Visibility = Visibility.Collapsed;
                    if (SelectedGroup == 1)
                    {
                        //Показать свойства видеокарты

                        infoCPUSectionName.Text = "InfoGPUSectionName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Visible;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Collapsed;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = GPUName;
                    }
                    if (SelectedGroup == 2)
                    {
                        //Показать свойства ОЗУ

                        infoCPUSectionName.Text = "InfoRAMSectionName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Visible;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = RAMName;
                        infoRAMMAINSection.Visibility = Visibility.Visible;
                        infoCPUMAINSection.Visibility = Visibility.Collapsed;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }
                    if (SelectedGroup == 3)
                    {
                        //Зона VRM

                        infoCPUSectionName.Text = "VRM";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Visible;
                        tbProcessor.Text = CPUName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }
                    if (SelectedGroup == 4)
                    {

                        infoCPUSectionName.Text = "InfoBatteryName".GetLocalized();
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = BATName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Visible;
                        InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    }
                    if (SelectedGroup == 5)
                    {

                        infoCPUSectionName.Text = "P-States";
                        InfoCPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                        tbProcessor.Text = CPUName;
                        infoRAMMAINSection.Visibility = Visibility.Collapsed;
                        infoCPUMAINSection.Visibility = Visibility.Visible;
                        InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                        InfoPSTSectionMetrics.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    InfoCPUComboBoxBorderSharedShadow_Element.Visibility = Visibility.Visible;
                    infoCPUMAINSection.Visibility = Visibility.Visible;
                    infoRAMMAINSection.Visibility = Visibility.Collapsed;
                    infoCPUSectionComboBox.Visibility = Visibility.Visible;
                    InfoCPUSectionMetrics.Visibility = Visibility.Visible;
                    InfoVRMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoGPUSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoRAMSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoBATSectionMetrics.Visibility = Visibility.Collapsed;
                    InfoPSTSectionMetrics.Visibility = Visibility.Collapsed;
                    //Скрыть лишние элементы 
                    infoCPUSectionName.Text = "InfoCPUSectionName".GetLocalized();
                    tbProcessor.Text = CPUName;
                }
                InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoARAMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));
                InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(60, 49));


                _ = RyzenADJWrapper.refresh_table(ryzenAccess);
                var batteryRate = GetSystemInfo.GetBatteryRate() / 1000;
                tbBATChargeRate.Text = $"{batteryRate}W";
                tbBAT.Text = GetSystemInfo.GetBatteryPercent() + "%";
                var batLifeTime = GetSystemInfo.GetBatteryLifeTime() + 0d;
                tbBATTime.Text = batLifeTime == -1 ? "InfoBatteryAC".GetLocalized() : $"{Math.Round(batLifeTime / 60 / 60, 0)}h {Math.Round((batLifeTime / 60 / 60 - Math.Round(batLifeTime / 60 / 60, 0)) * 60, 0)}m {Math.Round(((batLifeTime / 60 / 60 - (int)(batLifeTime / 60 / 60)) * 60 - Math.Round((batLifeTime / 60 / 60 - Math.Round(batLifeTime / 60 / 60, 0)) * 60, 0)) * 60, 0)}s".Replace('-', char.MinValue);
                InfoBATUsage.Text = tbBAT.Text + " " + tbBATChargeRate.Text + "\n" + tbBATTime.Text; infoIBATUsageBigBanner.Text = InfoBATUsage.Text;
                infoABATUsageBannerPolygonText.Text = tbBATChargeRate.Text; infoABATUsageBigBannerPolygonText.Text = tbBATChargeRate.Text;
                tbBATState.Text = GetSystemInfo.GetBatteryStatus().ToString();
                var currBatRate = batteryRate >= 0 ? batteryRate : -1 * batteryRate;
                var beforeMaxBatRate = MaxBatRate;
                if (MaxBatRate < currBatRate) { MaxBatRate = currBatRate; }
                tbStapmL.Text = Math.Round(RyzenADJWrapper.get_stapm_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_stapm_limit(ryzenAccess), 3) + "W";

                tbActualL.Text = Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_fast_limit(ryzenAccess), 3) + "W";
                tbAclualPowerL.Text = tbActualL.Text;

                tbAVGL.Text = Math.Round(RyzenADJWrapper.get_slow_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_slow_limit(ryzenAccess), 3) + "W";

                tbFast.Text = Math.Round(RyzenADJWrapper.get_stapm_time(ryzenAccess), 3) + "S";
                tbSlow.Text = Math.Round(RyzenADJWrapper.get_slow_time(ryzenAccess), 3) + "S";

                tbAPUL.Text = Math.Round(RyzenADJWrapper.get_apu_slow_value(ryzenAccess), 3) + "W/" + Math.Round(RyzenADJWrapper.get_apu_slow_limit(ryzenAccess), 3) + "W";

                tbVRMTDCL.Text = Math.Round(RyzenADJWrapper.get_vrm_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrm_current(ryzenAccess), 3) + "A";
                tbSOCTDCL.Text = Math.Round(RyzenADJWrapper.get_vrmsoc_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrmsoc_current(ryzenAccess), 3) + "A";
                tbVRMEDCL.Text = Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3) + "A";
                tbVRMEDCVRML.Text = tbVRMEDCL.Text;
                infoVRMUsageBanner.Text = Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "A\n" + Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3) + "W"; infoIVRMUsageBigBanner.Text = infoVRMUsageBanner.Text;
                infoAVRMUsageBannerPolygonText.Text = Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3) + "A"; infoAVRMUsageBigBannerPolygonText.Text = infoAVRMUsageBannerPolygonText.Text;
                tbSOCEDCL.Text = Math.Round(RyzenADJWrapper.get_vrmsocmax_current_value(ryzenAccess), 3) + "A/" + Math.Round(RyzenADJWrapper.get_vrmsocmax_current(ryzenAccess), 3) + "A";
                tbSOCVOLT.Text = Math.Round(RyzenADJWrapper.get_soc_volt(ryzenAccess), 3) + "V";
                tbSOCPOWER.Text = Math.Round(RyzenADJWrapper.get_soc_power(ryzenAccess), 3) + "W";
                tbMEMCLOCK.Text = Math.Round(RyzenADJWrapper.get_mem_clk(ryzenAccess), 3) + "InfoFreqBoundsMHZ".GetLocalized();
                tbFabricClock.Text = Math.Round(RyzenADJWrapper.get_fclk(ryzenAccess), 3) + "InfoFreqBoundsMHZ".GetLocalized();
                var core_Clk = 0f;
                var endtrace = 0;
                var core_Volt = 0f;
                var endtraced = 0;
                var maxFreq = 0.0d;
                var currentPstate = 4;
                for (uint f = 0; f < 8; f++)
                {
                    var getCurrFreq = RyzenADJWrapper.get_core_clk(ryzenAccess, f);
                    if (!float.IsNaN(getCurrFreq) && getCurrFreq > maxFreq)
                    {
                        maxFreq = getCurrFreq;
                    }
                    var currCore = infoCPUSectionComboBox.SelectedIndex switch
                    {
                        0 => getCurrFreq,
                        1 => RyzenADJWrapper.get_core_volt(ryzenAccess, f),
                        2 => RyzenADJWrapper.get_core_power(ryzenAccess, f),
                        3 => RyzenADJWrapper.get_core_temp(ryzenAccess, f),
                        _ => getCurrFreq
                    };
                    if (!float.IsNaN(currCore))
                    {
                        if (!InfoMainCPUFreqGrid.IsLoaded) { return; }
                        var currText = (TextBlock)InfoMainCPUFreqGrid.FindName($"FreqButtonText_{f}");
                        if (currText != null)
                        {
                            if (SelectedGroup == 0 || SelectedGroup == 5)
                            {
                                currText.Text = infoCPUSectionComboBox.SelectedIndex switch
                                {
                                    0 => Math.Round(currCore, 3) + " " + "infoAGHZ".GetLocalized(),
                                    1 => Math.Round(currCore, 3) + "V",
                                    2 => Math.Round(currCore, 3) + "W",
                                    3 => Math.Round(currCore, 3) + "C",
                                    _ => Math.Round(currCore, 3) + " " + "infoAGHZ".GetLocalized()
                                };
                            }
                            else
                            {
                                if (SelectedGroup == 1)
                                {
                                    foreach (var element in (new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM Win32_VideoController").Get().Cast<ManagementObject>()))
                                    {
                                        currText.Text = GetSystemInfo.GetGPUName((int)f);
                                    }
                                }

                                if (SelectedGroup == 2)
                                {
                                    var reject = 0;
                                    foreach (var element in tbRAMModel.Text.Split('/'))
                                    {
                                        if (reject == (int)f)
                                        {
                                            currText.Text = element;
                                        }
                                        reject++;
                                    }
                                }
                                if (SelectedGroup == 3)
                                {
                                    currText.Text = f == 0 ?
                                        $"{Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3)}A/{Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3)}A"
                                        : (f == 1 ? $"{Math.Round(RyzenADJWrapper.get_vrm_current_value(ryzenAccess), 3)}A/{Math.Round(RyzenADJWrapper.get_vrm_current(ryzenAccess), 3)}A"
                                        : (f == 2 ? $"{Math.Round(RyzenADJWrapper.get_vrmsocmax_current_value(ryzenAccess), 3)}A/{Math.Round(RyzenADJWrapper.get_vrmsocmax_current(ryzenAccess), 3)}A"
                                        : (f == 3 ? $"{Math.Round(RyzenADJWrapper.get_vrmsoc_current_value(ryzenAccess), 3)}A/{Math.Round(RyzenADJWrapper.get_vrmsoc_current(ryzenAccess), 3)}A" : $"{0}A")));
                                }
                                if (SelectedGroup == 4)
                                {
                                    currText.Text = BATName;
                                }
                            }
                        }
                        if (f < numberOfCores)
                        {
                            core_Clk += getCurrFreq;
                            endtrace += 1;
                        }
                    }
                    var currVolt = RyzenADJWrapper.get_core_volt(ryzenAccess, f);
                    if (!float.IsNaN(currVolt))
                    {
                        core_Volt += currVolt;
                        endtraced += 1;
                    }
                }
                if (endtrace != 0)
                {
                    tbCPUFreq.Text = Math.Round(core_Clk / endtrace, 3) + " " + "infoAGHZ".GetLocalized();

                    if (Math.Round(core_Clk / endtrace, 3) >= PSTatesList[2])
                    {
                        tbPST.Text = "P2"; infoAPSTUsageBannerPolygonText.Text = "P2"; infoAPSTUsageBigBannerPolygonText.Text = "P2"; currentPstate = 1;
                    }
                    else
                    {
                        tbPST.Text = "C1"; infoAPSTUsageBannerPolygonText.Text = "C1"; infoAPSTUsageBigBannerPolygonText.Text = "C1"; currentPstate = 0;
                    }
                    if (Math.Round(core_Clk / endtrace, 3) >= PSTatesList[1])
                    {
                        tbPST.Text = "P1"; infoAPSTUsageBannerPolygonText.Text = "P1"; infoAPSTUsageBigBannerPolygonText.Text = "P1"; currentPstate = 2;
                    }
                    if (Math.Round(core_Clk / endtrace, 3) >= PSTatesList[0])
                    {
                        tbPST.Text = "P0"; infoAPSTUsageBannerPolygonText.Text = "P0"; infoAPSTUsageBigBannerPolygonText.Text = "P0"; currentPstate = 3;
                    }
                    InfoPSTUsage.Text = tbPST.Text + "InfoPSTState".GetLocalized(); infoIPSTUsageBigBanner.Text = InfoPSTUsage.Text;
                }
                else
                {
                    tbCPUFreq.Text = "? " + "infoAGHZ".GetLocalized();
                }
                if (endtraced != 0)
                {
                    tbCPUVolt.Text = Math.Round(core_Volt / endtraced, 3) + "V";
                }
                else
                {
                    tbCPUVolt.Text = "?V";
                }
                tbPSTFREQ.Text = tbCPUFreq.Text;
                var gfxCLK = Math.Round(RyzenADJWrapper.get_gfx_clk(ryzenAccess) / 1000, 3);
                var gfxVolt = Math.Round(RyzenADJWrapper.get_gfx_volt(ryzenAccess), 3);
                var gfxTemp = RyzenADJWrapper.get_gfx_temp(ryzenAccess);
                var beforeMaxGFX = MaxGFXClock;
                if (MaxGFXClock < gfxCLK) { MaxGFXClock = gfxCLK; }
                infoGPUUsageBanner.Text = gfxCLK + " " + "infoAGHZ".GetLocalized() + "  " + Math.Round(gfxTemp, 0) + "C\n" + gfxVolt + "V";
                infoAGPUUsageBannerPolygonText.Text = gfxCLK + "infoAGHZ".GetLocalized(); tbGPUFreq.Text = infoAGPUUsageBannerPolygonText.Text;
                infoAGPUUsageBigBannerPolygonText.Text = infoAGPUUsageBannerPolygonText.Text; infoIGPUUsageBigBanner.Text = infoGPUUsageBanner.Text;
                tbGPUVolt.Text = gfxVolt + "V";
                var maxTemp = Math.Round(RyzenADJWrapper.get_tctl_temp(ryzenAccess), 3);
                tbCPUMaxL.Text = Math.Round(RyzenADJWrapper.get_tctl_temp_value(ryzenAccess), 3) + "C/" + maxTemp + "C";
                tbCPUMaxTempL.Text = tbCPUMaxL.Text; tbCPUMaxTempVRML.Text = tbCPUMaxL.Text;
                var apuTemp = Math.Round(RyzenADJWrapper.get_apu_skin_temp_value(ryzenAccess), 3);
                var apuTempLimit = Math.Round(RyzenADJWrapper.get_apu_skin_temp_limit(ryzenAccess), 3);
                tbAPUMaxL.Text = (!double.IsNaN(apuTemp) && apuTemp > 0 ? apuTemp : Math.Round(gfxTemp, 3)) + "C/" + (!double.IsNaN(apuTempLimit) && apuTempLimit > 0 ? apuTempLimit : maxTemp) + "C";
                tbDGPUMaxL.Text = Math.Round(RyzenADJWrapper.get_dgpu_skin_temp_value(ryzenAccess), 3) + "C/" + Math.Round(RyzenADJWrapper.get_dgpu_skin_temp_limit(ryzenAccess), 3) + "C";
                var CoreCPUUsage = Math.Round(RyzenADJWrapper.get_cclk_busy_value(ryzenAccess), 3);
                tbCPUUsage.Text = CoreCPUUsage + "%"; infoACPUUsageBannerPolygonText.Text = Math.Round(CoreCPUUsage, 0) + "%";
                infoICPUUsageBanner.Text = Math.Round(CoreCPUUsage, 0) + "%  " + tbCPUFreq.Text + "\n" + tbCPUVolt.Text;
                infoACPUUsageBigBannerPolygonText.Text = tbCPUUsage.Text; infoICPUUsageBigBanner.Text = infoICPUUsageBanner.Text;

                //InfoACPUBanner График
                InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                CPUPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(CoreCPUUsage * 0.48) });
                if (CPUFlyout.IsOpen)
                {
                    InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(CoreCPUUsage * 0.48)));
                }
                InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(CoreCPUUsage * 0.48)));
                foreach (var element in CPUPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            CPUPointer.Remove(element);
                            if (CPUFlyout.IsOpen)
                            {
                                InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            }
                            InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                        }
                        else
                        {
                            if (CPUFlyout.IsOpen)
                            {
                                InfoACPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            }
                            InfoACPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            element.X -= 1;
                            InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            if (CPUFlyout.IsOpen)
                            {
                                InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            }
                        }
                    }
                }
                if (CPUFlyout.IsOpen)
                {
                    InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }
                InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));


                //InfoAGPUBanner График
                InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                GPUPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(gfxCLK / MaxGFXClock * 48) });
                InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(gfxCLK / MaxGFXClock * 48)));
                if (GPUFlyout.IsOpen)
                {
                    InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(gfxCLK / MaxGFXClock * 48)));
                }
                foreach (var element in GPUPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            GPUPointer.Remove(element);
                            InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (GPUFlyout.IsOpen) { InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                        }
                        else
                        {
                            InfoAGPUBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (GPUFlyout.IsOpen) { InfoAGPUBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                            element.X -= 1;
                            element.Y = (int)(element.Y * beforeMaxGFX / MaxGFXClock);
                            if (GPUFlyout.IsOpen) { InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y)); }
                            InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }
                if (GPUFlyout.IsOpen) { InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49)); }
                InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));

                //InfoAVRMBanner График
                InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                VRMPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) / RyzenADJWrapper.get_vrmmax_current(ryzenAccess) * 48) });
                if (VRMFlyout.IsOpen)
                {
                    InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) / RyzenADJWrapper.get_vrmmax_current(ryzenAccess) * 48)));
                }
                InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) / RyzenADJWrapper.get_vrmmax_current(ryzenAccess) * 48)));
                foreach (var element in VRMPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            VRMPointer.Remove(element);
                            InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (VRMFlyout.IsOpen) { InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                        }
                        else
                        {
                            if (VRMFlyout.IsOpen) { InfoAVRMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                            InfoAVRMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            element.X -= 1;
                            if (VRMFlyout.IsOpen) { InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y)); }
                            InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                        }
                    }
                }
                InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                if (VRMFlyout.IsOpen) { InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49)); }

                //InfoAPSTBanner График
                InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                PSTPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(currentPstate * 16) });
                InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(currentPstate * 16)));
                if (PSTFlyout.IsOpen)
                {
                    InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(currentPstate * 16)));
                }
                foreach (var element in PSTPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            PSTPointer.Remove(element);
                            InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (PSTFlyout.IsOpen) { InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                        }
                        else
                        {
                            InfoAPSTBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (PSTFlyout.IsOpen) { InfoAPSTBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                            element.X -= 1;
                            InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            if (PSTFlyout.IsOpen) { InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y)); }
                        }
                    }
                }
                if (PSTFlyout.IsOpen) { InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49)); }
                InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));

                //InfoABATBanner График
                InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                BATPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(currBatRate / MaxBatRate * 48) });
                InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(currBatRate / MaxBatRate * 48)));
                if (BATFlyout.IsOpen)
                {
                    InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                    InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(currBatRate / MaxBatRate * 48)));
                }
                foreach (var element in BATPointer.ToList())
                {
                    if (element != null)
                    {
                        if (element.X < 0)
                        {
                            BATPointer.Remove(element);
                            InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (BATFlyout.IsOpen) { InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                        }
                        else
                        {
                            InfoABATBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                            if (BATFlyout.IsOpen) { InfoABATBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                            element.X -= 1;
                            element.Y = (int)(element.Y * beforeMaxBatRate / MaxBatRate);
                            InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                            if (BATFlyout.IsOpen) { InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y)); }
                        }
                    }
                }
                if (BATFlyout.IsOpen) { InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49)); }
                InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));


                var totalRam = 0d;
                var busyRam = 0d;
                //Раз в шесть секунд обновляет состояние памяти 
                var ramMonitor = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize,FreePhysicalMemory FROM Win32_OperatingSystem");
                foreach (var objram in ramMonitor.Get().Cast<ManagementObject>())
                {
                    totalRam = Convert.ToDouble(objram["TotalVisibleMemorySize"]);
                    busyRam = totalRam - Convert.ToDouble(objram["FreePhysicalMemory"]);
                    var RAMUsage = Math.Round(busyRam * 100 / totalRam, 0) + "%";
                    InfoRAMUsage.Text = RAMUsage + "\n" + Math.Round(busyRam / 1024 / 1024, 3) + "GB/" + Math.Round(totalRam / 1024 / 1024, 1) + "GB";
                    infoARAMUsageBannerPolygonText.Text = RAMUsage; infoARAMUsageBigBannerPolygonText.Text = infoARAMUsageBannerPolygonText.Text; infoIRAMUsageBigBanner.Text = InfoRAMUsage.Text;
                }

                //InfoARAMBanner График
                try
                {
                    if (busyRam != 0 && totalRam != 0)
                    {
                        InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                        RAMPointer.Add(new InfoPageCPUPoints() { X = 60, Y = 48 - (int)(busyRam * 100 / totalRam * 0.48) });
                        InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(busyRam * 100 / totalRam * 0.48)));
                        if (RAMFlyout.IsOpen)
                        {
                            InfoARAMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(0, 0));
                            InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 48 - (int)(busyRam * 100 / totalRam * 0.48)));
                        }
                    }
                    foreach (var element in RAMPointer.ToList())
                    {
                        if (element != null)
                        {
                            if (element.X < 0)
                            {
                                RAMPointer.Remove(element);
                                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                                if (RAMFlyout.IsOpen) { InfoARAMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                            }
                            else
                            {
                                if (RAMFlyout.IsOpen) { InfoARAMBigBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y)); }
                                InfoARAMBannerPolygon.Points.Remove(new Windows.Foundation.Point(element.X, element.Y));
                                element.X -= 1;
                                InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y));
                                if (RAMFlyout.IsOpen) { InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(element.X, element.Y)); }
                            }
                        }
                    }
                    if (RAMFlyout.IsOpen) { InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49)); }
                    InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(60, 49));
                }
                catch (Exception ex)
                {
                    SendSMUCommand.TraceIt_TraceError(ex.ToString());
                }
            }
            var avgCoreCLK = 0d;
            var avgCoreVolt = 0d;
            var endCLKString = "";
            var pattern = @"\$cpu_clock_cycle\$(.*?)\$cpu_clock_cycle_end\$";
            var match = Regex.Match(rtssset.AdvancedCodeEditor, pattern);
            for (var f = 0u; f < numberOfCores; f++)
            {
                if (f < 8)
                {
                    var clk = Math.Round(RyzenADJWrapper.get_core_clk(ryzenAccess, f), 3);
                    var volt = Math.Round(RyzenADJWrapper.get_core_volt(ryzenAccess, f), 3);
                    avgCoreCLK += clk;
                    avgCoreVolt += volt;
                    if (rtssset.AdvancedCodeEditor == "")
                    {
                        RtssLoad();
                    }
                    endCLKString += f > 3 ? "<Br>        " : "" + match.Groups[1].Value
                .Replace("$currCore$", f.ToString())
                .Replace("$cpu_core_clock$", clk.ToString())
                .Replace("$cpu_core_voltage$", volt.ToString());
                }
            }

            rtss_line = rtssset.AdvancedCodeEditor.Split("$cpu_clock_cycle$")[0].Replace("$SelectedProfile$", ShellPage.SelectedProfile.Replace('а', 'a').Replace('м', 'm').Replace('и', 'i').Replace('н', 'n').Replace('М', 'M').Replace('у', 'u').Replace('Э', 'E').Replace('о', 'o').Replace('Б', 'B').Replace('л', 'l').Replace('с', 'c').Replace('С', 'C').Replace('р', 'r').Replace('т', 't').Replace('ь', ' '))
                .Replace("$stapm_value$", Math.Round(RyzenADJWrapper.get_stapm_value(ryzenAccess), 3).ToString())
                .Replace("$stapm_limit$", Math.Round(RyzenADJWrapper.get_stapm_limit(ryzenAccess), 3).ToString())
                .Replace("$fast_value$", Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3).ToString())
                .Replace("$fast_limit$", Math.Round(RyzenADJWrapper.get_fast_limit(ryzenAccess), 3).ToString())
                .Replace("$slow_value$", Math.Round(RyzenADJWrapper.get_slow_value(ryzenAccess), 3).ToString())
                .Replace("$slow_limit$", Math.Round(RyzenADJWrapper.get_slow_limit(ryzenAccess), 3).ToString())
                .Replace("$vrmedc_value$", Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3).ToString())
                .Replace("$vrmedc_max$", Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3).ToString())
                .Replace("$cpu_temp_value$", Math.Round(RyzenADJWrapper.get_tctl_temp_value(ryzenAccess), 3).ToString())
                .Replace("$cpu_temp_max$", Math.Round(RyzenADJWrapper.get_tctl_temp(ryzenAccess), 3).ToString())
                .Replace("$cpu_usage$", Math.Round(RyzenADJWrapper.get_cclk_busy_value(ryzenAccess), 3).ToString())
                .Replace("$gfx_clock$", Math.Round(RyzenADJWrapper.get_gfx_clk(ryzenAccess), 3).ToString())
                .Replace("$gfx_volt$", Math.Round(RyzenADJWrapper.get_gfx_volt(ryzenAccess), 3).ToString())
                .Replace("$gfx_temp$", Math.Round(RyzenADJWrapper.get_gfx_temp(ryzenAccess), 3).ToString())
                .Replace("$average_cpu_clock$", Math.Round(avgCoreCLK / numberOfCores, 3).ToString())
                .Replace("$average_cpu_voltage$", Math.Round(avgCoreVolt / numberOfCores, 3).ToString())
                      + endCLKString
                      + rtssset.AdvancedCodeEditor.Split("$cpu_clock_cycle_end$")[1].Replace("$SelectedProfile$", ShellPage.SelectedProfile.Replace('а', 'a').Replace('м', 'm').Replace('и', 'i').Replace('н', 'n').Replace('М', 'M').Replace('у', 'u').Replace('Э', 'E').Replace('о', 'o').Replace('Б', 'B').Replace('л', 'l').Replace('с', 'c').Replace('С', 'C').Replace('р', 'r').Replace('т', 't').Replace('ь', ' '))
                .Replace("$stapm_value$", Math.Round(RyzenADJWrapper.get_stapm_value(ryzenAccess), 3).ToString())
                .Replace("$stapm_limit$", Math.Round(RyzenADJWrapper.get_stapm_limit(ryzenAccess), 3).ToString())
                .Replace("$fast_value$", Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3).ToString())
                .Replace("$fast_limit$", Math.Round(RyzenADJWrapper.get_fast_limit(ryzenAccess), 3).ToString())
                .Replace("$slow_value$", Math.Round(RyzenADJWrapper.get_slow_value(ryzenAccess), 3).ToString())
                .Replace("$slow_limit$", Math.Round(RyzenADJWrapper.get_slow_limit(ryzenAccess), 3).ToString())
                .Replace("$vrmedc_value$", Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3).ToString())
                .Replace("$vrmedc_max$", Math.Round(RyzenADJWrapper.get_vrmmax_current(ryzenAccess), 3).ToString())
                .Replace("$cpu_temp_value$", Math.Round(RyzenADJWrapper.get_tctl_temp_value(ryzenAccess), 3).ToString())
                .Replace("$cpu_temp_max$", Math.Round(RyzenADJWrapper.get_tctl_temp(ryzenAccess), 3).ToString())
                .Replace("$cpu_usage$", Math.Round(RyzenADJWrapper.get_cclk_busy_value(ryzenAccess), 3).ToString())
                .Replace("$gfx_clock$", Math.Round(RyzenADJWrapper.get_gfx_clk(ryzenAccess), 3).ToString())
                .Replace("$gfx_volt$", Math.Round(RyzenADJWrapper.get_gfx_volt(ryzenAccess), 3).ToString())
                .Replace("$gfx_temp$", Math.Round(RyzenADJWrapper.get_gfx_temp(ryzenAccess), 3).ToString())
                .Replace("$average_cpu_clock$", Math.Round(avgCoreCLK / numberOfCores, 3).ToString())
                .Replace("$average_cpu_voltage$", Math.Round(avgCoreVolt / numberOfCores, 3).ToString());


            if (niicons_Min_MaxValues[0].Min == 0.0f) { niicons_Min_MaxValues[0].Min = RyzenADJWrapper.get_stapm_value(ryzenAccess); }
            if (niicons_Min_MaxValues[1].Min == 0.0f) { niicons_Min_MaxValues[1].Min = RyzenADJWrapper.get_fast_value(ryzenAccess); }
            if (niicons_Min_MaxValues[2].Min == 0.0f) { niicons_Min_MaxValues[2].Min = RyzenADJWrapper.get_slow_value(ryzenAccess); }
            if (niicons_Min_MaxValues[3].Min == 0.0f) { niicons_Min_MaxValues[3].Min = RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess); }
            if (niicons_Min_MaxValues[4].Min == 0.0f) { niicons_Min_MaxValues[4].Min = RyzenADJWrapper.get_tctl_temp_value(ryzenAccess); }
            if (niicons_Min_MaxValues[5].Min == 0.0f) { niicons_Min_MaxValues[5].Min = RyzenADJWrapper.get_cclk_busy_value(ryzenAccess); }
            if (niicons_Min_MaxValues[6].Min == 0.0f) { niicons_Min_MaxValues[6].Min = (float)(avgCoreCLK / numberOfCores); }
            if (niicons_Min_MaxValues[7].Min == 0.0f) { niicons_Min_MaxValues[7].Min = (float)(avgCoreVolt / numberOfCores); }
            if (niicons_Min_MaxValues[8].Min == 0.0f) { niicons_Min_MaxValues[8].Min = RyzenADJWrapper.get_gfx_clk(ryzenAccess); }
            if (niicons_Min_MaxValues[9].Min == 0.0f) { niicons_Min_MaxValues[9].Min = RyzenADJWrapper.get_gfx_temp(ryzenAccess); }
            if (niicons_Min_MaxValues[10].Min == 0.0f) { niicons_Min_MaxValues[10].Min = RyzenADJWrapper.get_gfx_volt(ryzenAccess); }
            niicons_Min_MaxValues[0].Max = RyzenADJWrapper.get_stapm_value(ryzenAccess) > niicons_Min_MaxValues[0].Max ? RyzenADJWrapper.get_stapm_value(ryzenAccess) : niicons_Min_MaxValues[0].Max;
            niicons_Min_MaxValues[0].Min = RyzenADJWrapper.get_stapm_value(ryzenAccess) < niicons_Min_MaxValues[0].Min ? RyzenADJWrapper.get_stapm_value(ryzenAccess) : niicons_Min_MaxValues[0].Min;
            niicons_Min_MaxValues[1].Max = RyzenADJWrapper.get_fast_value(ryzenAccess) > niicons_Min_MaxValues[1].Max ? RyzenADJWrapper.get_fast_value(ryzenAccess) : niicons_Min_MaxValues[1].Max;
            niicons_Min_MaxValues[1].Min = RyzenADJWrapper.get_fast_value(ryzenAccess) < niicons_Min_MaxValues[1].Min ? RyzenADJWrapper.get_fast_value(ryzenAccess) : niicons_Min_MaxValues[1].Min;
            niicons_Min_MaxValues[2].Max = RyzenADJWrapper.get_slow_value(ryzenAccess) > niicons_Min_MaxValues[2].Max ? RyzenADJWrapper.get_slow_value(ryzenAccess) : niicons_Min_MaxValues[2].Max;
            niicons_Min_MaxValues[2].Min = RyzenADJWrapper.get_slow_value(ryzenAccess) < niicons_Min_MaxValues[2].Min ? RyzenADJWrapper.get_slow_value(ryzenAccess) : niicons_Min_MaxValues[2].Min;
            niicons_Min_MaxValues[3].Max = RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) > niicons_Min_MaxValues[3].Max ? RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) : niicons_Min_MaxValues[3].Max;
            niicons_Min_MaxValues[3].Min = RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) < niicons_Min_MaxValues[3].Min ? RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) : niicons_Min_MaxValues[3].Min;
            niicons_Min_MaxValues[4].Max = RyzenADJWrapper.get_tctl_temp_value(ryzenAccess) > niicons_Min_MaxValues[4].Max ? RyzenADJWrapper.get_tctl_temp_value(ryzenAccess) : niicons_Min_MaxValues[4].Max;
            niicons_Min_MaxValues[4].Min = RyzenADJWrapper.get_tctl_temp_value(ryzenAccess) < niicons_Min_MaxValues[4].Min ? RyzenADJWrapper.get_tctl_temp_value(ryzenAccess) : niicons_Min_MaxValues[4].Min;
            niicons_Min_MaxValues[5].Max = RyzenADJWrapper.get_cclk_busy_value(ryzenAccess) > niicons_Min_MaxValues[5].Max ? RyzenADJWrapper.get_cclk_busy_value(ryzenAccess) : niicons_Min_MaxValues[5].Max;
            niicons_Min_MaxValues[5].Min = RyzenADJWrapper.get_cclk_busy_value(ryzenAccess) < niicons_Min_MaxValues[5].Min ? RyzenADJWrapper.get_cclk_busy_value(ryzenAccess) : niicons_Min_MaxValues[5].Min;
            niicons_Min_MaxValues[6].Max = (float)((avgCoreCLK / numberOfCores) > niicons_Min_MaxValues[6].Max ? (avgCoreCLK / numberOfCores) : niicons_Min_MaxValues[6].Max);
            niicons_Min_MaxValues[6].Min = (float)((avgCoreCLK / numberOfCores) < niicons_Min_MaxValues[6].Min ? (avgCoreCLK / numberOfCores) : niicons_Min_MaxValues[6].Min);
            niicons_Min_MaxValues[7].Max = (float)((avgCoreVolt / numberOfCores) > niicons_Min_MaxValues[7].Max ? (avgCoreVolt / numberOfCores) : niicons_Min_MaxValues[7].Max);
            niicons_Min_MaxValues[7].Min = (float)((avgCoreVolt / numberOfCores) < niicons_Min_MaxValues[7].Min ? (avgCoreVolt / numberOfCores) : niicons_Min_MaxValues[7].Min);
            niicons_Min_MaxValues[8].Max = RyzenADJWrapper.get_gfx_clk(ryzenAccess) > niicons_Min_MaxValues[8].Max ? RyzenADJWrapper.get_gfx_clk(ryzenAccess) : niicons_Min_MaxValues[8].Max;
            niicons_Min_MaxValues[8].Min = RyzenADJWrapper.get_gfx_clk(ryzenAccess) < niicons_Min_MaxValues[8].Min ? RyzenADJWrapper.get_gfx_clk(ryzenAccess) : niicons_Min_MaxValues[8].Min;
            niicons_Min_MaxValues[9].Max = RyzenADJWrapper.get_gfx_temp(ryzenAccess) > niicons_Min_MaxValues[9].Max ? RyzenADJWrapper.get_gfx_temp(ryzenAccess) : niicons_Min_MaxValues[9].Max;
            niicons_Min_MaxValues[9].Min = RyzenADJWrapper.get_gfx_temp(ryzenAccess) < niicons_Min_MaxValues[9].Min ? RyzenADJWrapper.get_gfx_temp(ryzenAccess) : niicons_Min_MaxValues[9].Min;
            niicons_Min_MaxValues[10].Max = RyzenADJWrapper.get_gfx_volt(ryzenAccess) > niicons_Min_MaxValues[10].Max ? RyzenADJWrapper.get_gfx_volt(ryzenAccess) : niicons_Min_MaxValues[10].Max;
            niicons_Min_MaxValues[10].Min = RyzenADJWrapper.get_gfx_volt(ryzenAccess) < niicons_Min_MaxValues[10].Min ? RyzenADJWrapper.get_gfx_volt(ryzenAccess) : niicons_Min_MaxValues[10].Min;

            Change_Ni_Icons_Text("Settings_ni_Values_STAPM", Math.Round(RyzenADJWrapper.get_stapm_value(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_STAPM".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_stapm_value(ryzenAccess) + "W", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[0].Min.ToString() + "W" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[0].Max.ToString() + "W");
            Change_Ni_Icons_Text("Settings_ni_Values_Fast", Math.Round(RyzenADJWrapper.get_fast_value(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_Fast".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_fast_value(ryzenAccess) + "W", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[1].Min.ToString() + "W" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[1].Max.ToString() + "W");
            Change_Ni_Icons_Text("Settings_ni_Values_Slow", Math.Round(RyzenADJWrapper.get_slow_value(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_Slow".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_slow_value(ryzenAccess) + "W", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[2].Min.ToString() + "W" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[2].Max.ToString() + "W");
            Change_Ni_Icons_Text("Settings_ni_Values_VRMEDC", Math.Round(RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_VRMEDC".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_vrmmax_current_value(ryzenAccess) + "A", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[3].Min.ToString() + "A" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[3].Max.ToString() + "A");
            Change_Ni_Icons_Text("Settings_ni_Values_CPUTEMP", Math.Round(RyzenADJWrapper.get_tctl_temp_value(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_CPUTEMP".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_tctl_temp_value(ryzenAccess) + "C", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[4].Min.ToString() + "C" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[4].Max.ToString() + "C");
            Change_Ni_Icons_Text("Settings_ni_Values_CPUUsage", Math.Round(RyzenADJWrapper.get_cclk_busy_value(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_CPUUsage".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_cclk_busy_value(ryzenAccess) + "%", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[5].Min.ToString() + "%" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[5].Max.ToString() + "%");
            Change_Ni_Icons_Text("Settings_ni_Values_AVGCPUCLK", Math.Round(avgCoreCLK / numberOfCores, 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_AVGCPUCLK".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + avgCoreCLK / numberOfCores + "GHz", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[6].Min.ToString() + "GHz" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[6].Max.ToString() + "GHz");
            Change_Ni_Icons_Text("Settings_ni_Values_AVGCPUVOLT", Math.Round(avgCoreVolt / numberOfCores, 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_AVGCPUVOLT".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + avgCoreVolt / numberOfCores + "V", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[7].Min.ToString() + "V" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[7].Max.ToString() + "V");
            Change_Ni_Icons_Text("Settings_ni_Values_GFXCLK", Math.Round(RyzenADJWrapper.get_gfx_clk(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_GFXCLK".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_gfx_clk(ryzenAccess) + "MHz", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[8].Min.ToString() + "MHz" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[8].Max.ToString() + "MHz");
            Change_Ni_Icons_Text("Settings_ni_Values_GFXTEMP", Math.Round(RyzenADJWrapper.get_gfx_temp(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_GFXTEMP".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_gfx_temp(ryzenAccess) + "C", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[9].Min.ToString() + "C" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[9].Max.ToString() + "C");
            Change_Ni_Icons_Text("Settings_ni_Values_GFXVOLT", Math.Round(RyzenADJWrapper.get_gfx_volt(ryzenAccess), 3).ToString(), "Saku Overclock© -\nTrayMon\n" + "Settings_ni_Values_GFXVOLT".GetLocalized() + "Settings_ni_Values_CurrentValue".GetLocalized() + RyzenADJWrapper.get_gfx_volt(ryzenAccess) + "V", "Settings_ni_Values_MinValue".GetLocalized() + niicons_Min_MaxValues[10].Min.ToString() + "V" + "Settings_ni_Values_MaxValue".GetLocalized() + niicons_Min_MaxValues[10].Max.ToString() + "V");

            if (infoRTSSButton.IsChecked == true)
            {
                RTSSHandler.ChangeOSDText(rtss_line);
            }
        }
        catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
    }
    // Автообновление информации 
    private void StartInfoUpdate()
    {
        try
        {
            InfoACPUBannerPolygon.Points.Clear();
            InfoACPUBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoACPUBigBannerPolygon.Points.Clear();
            InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAGPUBannerPolygon.Points.Clear();
            InfoAGPUBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAGPUBigBannerPolygon.Points.Clear();
            InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoARAMBannerPolygon.Points.Clear();
            InfoARAMBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoARAMBigBannerPolygon.Points.Clear();
            InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAVRMBannerPolygon.Points.Clear();
            InfoAVRMBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAVRMBigBannerPolygon.Points.Clear();
            InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoABATBannerPolygon.Points.Clear();
            InfoABATBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoABATBigBannerPolygon.Points.Clear();
            InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAPSTBannerPolygon.Points.Clear();
            InfoAPSTBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            InfoAPSTBigBannerPolygon.Points.Clear();
            InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            ryzenAccess = RyzenADJWrapper.Init_ryzenadj();
            _ = RyzenADJWrapper.Init_Table(ryzenAccess);
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += (sender, e) => UpdateInfoAsync();
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(300);
            App.MainWindow.VisibilityChanged += Window_VisibilityChanged;
            dispatcherTimer.Start();
        }
        catch (Exception ex)
        {
            SendSMUCommand.TraceIt_TraceError(ex.ToString());
        }
    }
    // Метод, который будет вызываться при скрытии/переключении страницы
    private void StopInfoUpdate()
    {
        dispatcherTimer?.Stop();
    }
    private void ИнформацияPage_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            DisposeAllNotifyIcons();
        }
        catch (Exception ex)
        {
            SendSMUCommand.TraceIt_TraceError(ex.ToString());
        }
        try
        {
            infoRTSSButton.IsChecked = false;
            dispatcherTimer?.Stop();
            RTSSHandler.ResetOSDText();
        }
        catch (Exception ex)
        {
            SendSMUCommand.TraceIt_TraceError(ex.ToString());
        }
    }
    #endregion

    #region Event Handlers
    private void InfoCPUSectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        //0 - частота, 1 - напряжение, 2 - мощность, 3 - температуры
        if (!loaded) { return; }
        InfoMainCPUFreqGrid.Children.Clear();
        InfoCPUSectionGridBuilder();
    }

    private void CPUBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup != 0)
        {
            if (CPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow ??= new ThemeShadow();
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }
            SelectedGroup = 0;
            CPUBannerButton.Background = SelectedBrush;
            CPUBannerButton.BorderBrush = SelectedBorderBrush;
            GPUBannerButton.Background = TransparentBrush;
            GPUBannerButton.BorderBrush = TransparentBrush;
            RAMBannerButton.Background = TransparentBrush;
            RAMBannerButton.BorderBrush = TransparentBrush;
            VRMBannerButton.Background = TransparentBrush;
            VRMBannerButton.BorderBrush = TransparentBrush;
            BATBannerButton.Background = TransparentBrush;
            BATBannerButton.BorderBrush = TransparentBrush;
            PSTBannerButton.Background = TransparentBrush;
            PSTBannerButton.BorderBrush = TransparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            InfoCPUSectionGridBuilder();
        }
    }

    private void GPUBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup != 1)
        {
            if (GPUBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow ??= new ThemeShadow();
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }
            SelectedGroup = 1;
            CPUBannerButton.Background = TransparentBrush;
            CPUBannerButton.BorderBrush = TransparentBrush;
            GPUBannerButton.Background = SelectedBrush;
            GPUBannerButton.BorderBrush = SelectedBorderBrush;
            RAMBannerButton.Background = TransparentBrush;
            RAMBannerButton.BorderBrush = TransparentBrush;
            VRMBannerButton.Background = TransparentBrush;
            VRMBannerButton.BorderBrush = TransparentBrush;
            BATBannerButton.Background = TransparentBrush;
            BATBannerButton.BorderBrush = TransparentBrush;
            PSTBannerButton.Background = TransparentBrush;
            PSTBannerButton.BorderBrush = TransparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            InfoCPUSectionGridBuilder();
        }
    }

    private void RAMBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup != 2)
        {
            if (RAMBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow ??= new ThemeShadow();
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }
            SelectedGroup = 2;
            CPUBannerButton.Background = TransparentBrush;
            CPUBannerButton.BorderBrush = TransparentBrush;
            GPUBannerButton.Background = TransparentBrush;
            GPUBannerButton.BorderBrush = TransparentBrush;
            RAMBannerButton.Background = SelectedBrush;
            RAMBannerButton.BorderBrush = SelectedBorderBrush;
            VRMBannerButton.Background = TransparentBrush;
            VRMBannerButton.BorderBrush = TransparentBrush;
            BATBannerButton.Background = TransparentBrush;
            BATBannerButton.BorderBrush = TransparentBrush;
            PSTBannerButton.Background = TransparentBrush;
            PSTBannerButton.BorderBrush = TransparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            InfoCPUSectionGridBuilder();
        }
    }
    private void VRMBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup != 3)
        {
            if (VRMBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow ??= new ThemeShadow();
            }
            SelectedGroup = 3;
            CPUBannerButton.Background = TransparentBrush;
            CPUBannerButton.BorderBrush = TransparentBrush;
            GPUBannerButton.Background = TransparentBrush;
            GPUBannerButton.BorderBrush = TransparentBrush;
            RAMBannerButton.Background = TransparentBrush;
            RAMBannerButton.BorderBrush = TransparentBrush;
            VRMBannerButton.Background = SelectedBrush;
            VRMBannerButton.BorderBrush = SelectedBorderBrush;
            BATBannerButton.Background = TransparentBrush;
            BATBannerButton.BorderBrush = TransparentBrush;
            PSTBannerButton.Background = TransparentBrush;
            PSTBannerButton.BorderBrush = TransparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            InfoCPUSectionGridBuilder();
        }
    }
    private void BATBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup != 4)
        {
            if (BATBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow ??= new ThemeShadow();
                PSTBannerButton.Shadow = null;
                VRMBannerButton.Shadow = null;
            }
            SelectedGroup = 4;
            CPUBannerButton.Background = TransparentBrush;
            CPUBannerButton.BorderBrush = TransparentBrush;
            GPUBannerButton.Background = TransparentBrush;
            GPUBannerButton.BorderBrush = TransparentBrush;
            RAMBannerButton.Background = TransparentBrush;
            RAMBannerButton.BorderBrush = TransparentBrush;
            VRMBannerButton.Background = TransparentBrush;
            VRMBannerButton.BorderBrush = TransparentBrush;
            BATBannerButton.Background = SelectedBrush;
            BATBannerButton.BorderBrush = SelectedBorderBrush;
            PSTBannerButton.Background = TransparentBrush;
            PSTBannerButton.BorderBrush = TransparentBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            InfoCPUSectionGridBuilder();
        }
    }
    private void PSTBannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedGroup != 5)
        {
            if (PSTBannerButton.Shadow != new ThemeShadow())
            {
                CPUBannerButton.Shadow = null;
                GPUBannerButton.Shadow = null;
                RAMBannerButton.Shadow = null;
                BATBannerButton.Shadow = null;
                PSTBannerButton.Shadow ??= new ThemeShadow();
                VRMBannerButton.Shadow = null;
            }
            SelectedGroup = 5;
            CPUBannerButton.Background = TransparentBrush;
            CPUBannerButton.BorderBrush = TransparentBrush;
            GPUBannerButton.Background = TransparentBrush;
            GPUBannerButton.BorderBrush = TransparentBrush;
            RAMBannerButton.Background = TransparentBrush;
            RAMBannerButton.BorderBrush = TransparentBrush;
            VRMBannerButton.Background = TransparentBrush;
            VRMBannerButton.BorderBrush = TransparentBrush;
            BATBannerButton.Background = TransparentBrush;
            BATBannerButton.BorderBrush = TransparentBrush;
            PSTBannerButton.Background = SelectedBrush;
            PSTBannerButton.BorderBrush = SelectedBorderBrush;
            InfoMainCPUFreqGrid.Children.Clear();
            if (infoCPUSectionComboBox.SelectedIndex != 0)
            {
                infoCPUSectionComboBox.SelectedIndex = 0;
            }
            else
            {
                InfoCPUSectionGridBuilder();
            }
        }
    }
    private async void InfoRTSSButton_Click(object sender, RoutedEventArgs e)
    {
        if (infoRTSSButton.IsChecked == false)
        {
            RTSSHandler.ResetOSDText();
        }
        else
        {
            Info_RTSSTeacherTip.IsOpen = true;
            await Task.Delay(3000);
            Info_RTSSTeacherTip.IsOpen = false;
        }
        if (!loaded) { return; }
        ConfigLoad();
        config.RTSSMetricsEnabled = infoRTSSButton.IsChecked == true;
        ConfigSave();
    }
    private void infoNiIconsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!loaded) { return; }
        if (infoNiIconsButton.IsChecked == true)
        {
            CreateNotifyIcons();
        }
        else
        {
            DisposeAllNotifyIcons();
        }
        ConfigLoad();
        config.NiIconsEnabled = infoNiIconsButton.IsChecked == true;
        ConfigSave();
    }
    private void CPUFlyout_Opening(object sender, object e)
    {
        InfoACPUBigBannerPolygon.Points.Clear();
        InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (CPUFlyout.IsOpen)
        {
            InfoACPUBigBannerPolygon.Points.Clear();
            InfoACPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            CPU_Expand_FontIcon.Glyph = "\uF0D7";
            CPU_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            CPU_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }
    private void CPUBannerButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (CPUBannerButton.IsPointerOver == true)
        {
            CPU_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Visible;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (CPUFlyout.IsOpen)
            {
                CPU_Expand_Button.Visibility = Visibility.Visible;
                CPU_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                CPU_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void VRMFlyout_Opening(object sender, object e)
    {
        InfoAVRMBigBannerPolygon.Points.Clear();
        InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (VRMFlyout.IsOpen)
        {
            InfoAVRMBigBannerPolygon.Points.Clear();
            InfoAVRMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            VRM_Expand_FontIcon.Glyph = "\uF0D7";
            VRM_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }
    private void VRMBannerButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (VRMBannerButton.IsPointerOver == true)
        {
            VRM_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Visible;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (VRMFlyout.IsOpen)
            {
                VRM_Expand_Button.Visibility = Visibility.Visible;
                VRM_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                VRM_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void GPUFlyout_Opening(object sender, object e)
    {
        InfoAGPUBigBannerPolygon.Points.Clear();
        InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (GPUFlyout.IsOpen)
        {
            InfoAGPUBigBannerPolygon.Points.Clear();
            InfoAGPUBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            GPU_Expand_FontIcon.Glyph = "\uF0D7";
            GPU_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }
    private void GPUBannerButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (GPUBannerButton.IsPointerOver == true)
        {
            GPU_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Visible;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (GPUFlyout.IsOpen)
            {
                GPU_Expand_Button.Visibility = Visibility.Visible;
                GPU_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                GPU_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void RAMFlyout_Opening(object sender, object e)
    {
        InfoARAMBigBannerPolygon.Points.Clear();
        InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (RAMFlyout.IsOpen)
        {
            InfoARAMBigBannerPolygon.Points.Clear();
            InfoARAMBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            RAM_Expand_FontIcon.Glyph = "\uF0D7";
            RAM_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }
    private void RAMBannerButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (RAMBannerButton.IsPointerOver == true)
        {
            RAM_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Visible;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (RAMFlyout.IsOpen)
            {
                RAM_Expand_Button.Visibility = Visibility.Visible;
                RAM_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                RAM_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void BATFlyout_Opening(object sender, object e)
    {
        InfoABATBigBannerPolygon.Points.Clear();
        InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (BATFlyout.IsOpen)
        {
            InfoABATBigBannerPolygon.Points.Clear();
            InfoABATBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            BAT_Expand_FontIcon.Glyph = "\uF0D7";
            BAT_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }
    private void BATBannerButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (BATBannerButton.IsPointerOver == true)
        {
            BAT_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Visible;
            PST_Expand_Button.Visibility = Visibility.Collapsed;
        }
        else
        {
            if (BATFlyout.IsOpen)
            {
                BAT_Expand_Button.Visibility = Visibility.Visible;
                BAT_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                BAT_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }
    private void PSTFlyout_Opening(object sender, object e)
    {
        InfoAPSTBigBannerPolygon.Points.Clear();
        InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
        if (PSTFlyout.IsOpen)
        {
            InfoAPSTBigBannerPolygon.Points.Clear();
            InfoAPSTBigBannerPolygon.Points.Add(new Windows.Foundation.Point(0, 49));
            PST_Expand_FontIcon.Glyph = "\uF0D7";
            PST_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            PST_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_FontIcon.Glyph = "\uF0D8";
        }
    }
    private void PSTBannerButton_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (PSTBannerButton.IsPointerOver == true)
        {
            PST_Expand_FontIcon.Glyph = "\uF0D8";
            CPU_Expand_Button.Visibility = Visibility.Collapsed;
            VRM_Expand_Button.Visibility = Visibility.Collapsed;
            GPU_Expand_Button.Visibility = Visibility.Collapsed;
            RAM_Expand_Button.Visibility = Visibility.Collapsed;
            BAT_Expand_Button.Visibility = Visibility.Collapsed;
            PST_Expand_Button.Visibility = Visibility.Visible;
        }
        else
        {
            if (PSTFlyout.IsOpen)
            {
                PST_Expand_Button.Visibility = Visibility.Visible;
                PST_Expand_FontIcon.Glyph = "\uF0D7";
            }
            else
            {
                PST_Expand_Button.Visibility = Visibility.Collapsed;
            }
        }
    }
    #endregion 
}