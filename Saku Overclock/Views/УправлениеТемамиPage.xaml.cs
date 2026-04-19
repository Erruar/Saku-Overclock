using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Text;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Views;

public sealed partial class УправлениеТемамиPage
{
    private readonly IThemeSelectorService
        _themeSelectorService = App.GetService<IThemeSelectorService>(); // Темы приложения

    private readonly IAppSettingsService _appSettings = App.GetService<IAppSettingsService>(); // Настройки

    private readonly ILocalThemeSettingsService
        _localThemeSettings = App.GetService<ILocalThemeSettingsService>(); // Дефолтные темы

    private readonly INavigationService _navigationService = App.GetService<INavigationService>(); // Навигация

    private readonly IAppNotificationService
        _notificationsService = App.GetService<IAppNotificationService>(); // Уведомления

    private bool _isLoading = true; // Состояние загрузки
    private Button? _selectedCardBtn; // Выбранный элемент

    public УправлениеТемамиPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
        Loaded += OnLoaded;
    }

    #region Init

    /// <summary>
    ///     Загружает тексты в верхний бар навигации
    /// </summary>
    private void SetupBreadcrumb()
    {
        PageBreadcrumb.ItemsSource = new List<string>
        {
            "Settings_Name/Text".GetLocalized(), // "Настройки" (локализованная строка)
            "ThemeManager/Text".GetLocalized() // "Управление темами"
        };
    }

    /// <summary>
    ///     Страница загружена
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadThemeCards();
    }

    /// <summary>
    ///     Заполняет левую панель карточками с темами и прокручивает до нужного места активную карточку
    /// </summary>
    private void LoadThemeCards()
    {
        ThemeCardsPanel.Children.Clear();
        _selectedCardBtn = null;

        for (var i = 0; i < _themeSelectorService.Themes.Count; i++)
        {
            var index = i; // Сохраняем копию индекса
            var theme = _themeSelectorService.Themes[i];
            var isActive = i == _appSettings.ThemeType;

            // Строим карточку темы
            var maskBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = theme.ThemeLight
                    ? new SolidColorBrush
                    {
                        Color = Color.FromArgb(255, 249, 249, 249)
                    }
                    : new SolidColorBrush
                    {
                        Color = Color.FromArgb(255, 39, 39, 39)
                    },
                Opacity = index is > -1 and < 3 ? 0 : 1
            };

            var bgBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = theme.ThemeOpacity
            };

            // Если путь к изображению доступен, устанавливает фоновое изображение
            if (!string.IsNullOrEmpty(theme.ThemeBackground))
                try
                {
                    bgBorder.Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(theme.ThemeBackground)),
                        Stretch = Stretch.UniformToFill
                    };
                }
                catch
                {
                    /* Нет фона */
                }

            switch (index)
            {
                case 0:
                    bgBorder.Background = new LinearGradientBrush
                    {
                        StartPoint = new Point(-0.2, -0.3), // Левый верхний угол
                        EndPoint = new Point(0.6, 2), // Правый нижний угол
                        GradientStops =
                        {
                            new GradientStop
                            {
                                Color = Color.FromArgb(255, 249, 249, 249), // Белый
                                Offset = 0.5
                            },
                            new GradientStop
                            {
                                Color = Color.FromArgb(255, 39, 39, 39), // Серый
                                Offset = 0.5
                            }
                        }
                    };
                    bgBorder.Opacity = 1;
                    break;
                case 1:
                    bgBorder.Background = new SolidColorBrush
                    {
                        Color = Color.FromArgb(255, 249, 249, 249)
                    };
                    bgBorder.Opacity = 1;
                    break;
                case 2:
                    bgBorder.Background = new SolidColorBrush
                    {
                        Color = Color.FromArgb(255, 39, 39, 39)
                    };
                    bgBorder.Opacity = 1;
                    break;
            }

            var maskGrid = new Grid
            {
                RequestedTheme = theme.ThemeLight ? ElementTheme.Light : ElementTheme.Dark,
                CornerRadius = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Opacity = index is > -1 and < 3 ? 0 : theme.ThemeMaskOpacity,
                Background = (Brush)Application.Current.Resources["BackgroundImageMaskAcrylicBrush"]
            };

            // Имя темы до 16 символов
            var displayName = theme.ThemeName.Length > 16
                ? theme.ThemeName[..16] + "…"
                : theme.ThemeName;

            try
            {
                displayName = displayName.Contains("Theme_") ? displayName.GetLocalized() : displayName;
            }
            catch
            {
                /* Не переводить */
            }

            var nameBtn = new Button
            {
                RequestedTheme = theme.ThemeLight ? ElementTheme.Light :
                    index == 0 ? ElementTheme.Light : ElementTheme.Dark,
                Content = displayName,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                CornerRadius = new CornerRadius(10),
                MaxWidth = 180,
                Shadow = SharedShadow,
                Translation = new Vector3(0, 0, 20),
                IsHitTestVisible = false // Кликать на карточку, а не на кнопку
            };

            var cardGrid = new Grid
            {
                Width = 196,
                Height = 110
            };
            cardGrid.Children.Add(maskBorder);
            cardGrid.Children.Add(bgBorder);
            cardGrid.Children.Add(maskGrid);
            cardGrid.Children.Add(nameBtn);

            // Кнопка выделения карточки
            var card = new Button
            {
                Tag = index,
                Content = cardGrid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(14),
                Margin = i == 0 ? new Thickness(0) : new Thickness(0, 6, 0, 0),
                BorderThickness = isActive ? new Thickness(2) : new Thickness(0),
                BorderBrush = isActive
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : null
            };

            if (index > 7)
            {
                card.ContextFlyout = new MenuBarItemFlyout
                {
                    Items =
                    {
                        new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE70F"},
                            Text = "Rename".GetLocalized(),
                            Command = new RelayCommand(() => { RenameTheme_Click((int)card.Tag); })
                        },
                        new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE74D"},
                            Text = "Delete".GetLocalized(),
                            Command = new RelayCommand(() => { DeleteTheme_Click((int)card.Tag); })
                        }
                    }
                };
            }

            card.Click += (_, _) => SelectTheme(index);

            if (isActive) _selectedCardBtn = card;

            ThemeCardsPanel.Children.Add(card);
        }

        // Прокрутит карточки до активной
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            if (_selectedCardBtn != null) _selectedCardBtn.StartBringIntoView();
        });

        RefreshRightPanel();
    }

    #endregion

    #region Theme Selection

    /// <summary>
    ///     Выбирает тему: выделяет карточку, применяет тему, обновляет правую панель настроек
    /// </summary>
    private void SelectTheme(int index)
    {
        if (index < 0 || index >= _themeSelectorService.Themes.Count) return;

        // Обновляет рамку на всех карточках, с учётом выделенной
        for (var i = 0; i < ThemeCardsPanel.Children.Count; i++)
            if (ThemeCardsPanel.Children[i] is Button btn)
            {
                var isSelected = i == index;
                btn.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
                btn.BorderBrush = isSelected
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : null;
                if (isSelected) _selectedCardBtn = btn;
            }

        _appSettings.ThemeType = index;
        _appSettings.SaveSettings();

        // Применяет тему
        var selected = _themeSelectorService.Themes[index];
        if (index == 0)
            _themeSelectorService.SetThemeAsync(ElementTheme.Default);
        else
            _themeSelectorService.SetThemeAsync(selected.ThemeLight ? ElementTheme.Light : ElementTheme.Dark);

        UpdateTheme();
        RefreshRightPanel();
    }

    private void UpdateTheme()
    {
        _notificationsService.ShowNotification("Theme applied!",
            "DEBUG MESSAGE. YOU SHOULDN'T SEE THIS",
            InfoBarSeverity.Success);
    }

    /// <summary>
    ///     Обновляет значения видимости параметров правой панели настроек для текущей темы
    /// </summary>
    private void RefreshRightPanel()
    {
        var idx = _appSettings.ThemeType;

        // Темы 0–2 - InfoBar, не редактируются
        var isBuiltIn = idx <= 2;
        BuiltInThemeInfoBar.IsOpen = isBuiltIn;
        ThemeEditPanel.Visibility = isBuiltIn ? Visibility.Collapsed : Visibility.Visible;
        NoSelectionText.Visibility = Visibility.Collapsed;

        if (isBuiltIn || idx >= _themeSelectorService.Themes.Count) return;

        // Загрузка значений без сохранения
        _isLoading = true;

        var t = _themeSelectorService.Themes[idx];
        ThemeOpacitySlider.Value = t.ThemeOpacity;
        ThemeMaskOpacitySlider.Value = t.ThemeMaskOpacity;
        ThemeTypeComboBox.SelectedIndex = t.ThemeLight ? 1 : 0;

        var showBgOptions = idx > 7 ? Visibility.Visible : Visibility.Collapsed;
        ThemeTypeComboBox.Visibility = showBgOptions;
        ThemeBgBtn.Visibility = showBgOptions;
        _isLoading = false;
    }

    private bool CheckCanEdit()
    {
        return !_isLoading
               && _appSettings.ThemeType > 2
               && _appSettings.ThemeType < _themeSelectorService.Themes.Count;
    }

    #endregion

    #region Right Panel Event Handlers

    private void ThemeColorIntensity_ValueChanged(object sender, object args)
    {
        if (!CheckCanEdit()) return;
        _themeSelectorService.Themes[_appSettings.ThemeType].ThemeOpacity = ThemeOpacitySlider.Value;
        _themeSelectorService.SaveThemeInSettings();
        RefreshCardPreview(_appSettings.ThemeType);
    }

    private void ThemeBackgroundMaskOpacity_ValueChanged(object sender, object args)
    {
        if (!CheckCanEdit()) return;
        _themeSelectorService.Themes[_appSettings.ThemeType].ThemeMaskOpacity = ThemeMaskOpacitySlider.Value;
        _themeSelectorService.SaveThemeInSettings();
        RefreshCardPreview(_appSettings.ThemeType);
    }

    private void ThemeType_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (!CheckCanEdit()) return;
        _themeSelectorService.Themes[_appSettings.ThemeType].ThemeLight = ThemeTypeComboBox.SelectedIndex == 1;
        _themeSelectorService.SaveThemeInSettings();
        _themeSelectorService.SetThemeAsync(ThemeTypeComboBox.SelectedIndex == 1
            ? ElementTheme.Light
            : ElementTheme.Dark);
    }

    #endregion

    #region Background Picker

    private async void OpenSelectThemeBackground_Click(object sender, object e)
    {
        try
        {
            if (!CheckCanEdit()) return;

            var endPath = "";
            var isFileMode = true; // Отслеживаем выбранный режим

            // 1. Корневой контейнер с ограничением ширины, чтобы диалог не разъезжался
            var rootPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 16,
                MaxWidth = 450,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // 2. Радио-кнопки для выбора источника
            var modeSelector = new RadioButtons
            {
                Header = "ThemeBgSelectSource".GetLocalized()
            };
            var rbNone = new RadioButton { Content = "None", IsChecked = true };
            var rbFile = new RadioButton { Content = "ThemeBgFromFile".GetLocalized() };
            var rbLink = new RadioButton { Content = "ThemeBgFromURL".GetLocalized() };
            modeSelector.Items.Add(rbNone);
            modeSelector.Items.Add(rbFile);
            modeSelector.Items.Add(rbLink);

            // 3. Панель для локального файла
            var filePanel = new StackPanel { Spacing = 8 };
            var fileDesc = new TextBlock
            {
                Width = 303,
                Text = "ThemeBgFromFileWhy".GetLocalized(),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            var browseButton = new Button { Content = "ThemeBgSourceBrowse".GetLocalized() };
            var filePickedText = new TextBlock
            {
                Width = 303,
                Visibility = Visibility.Collapsed,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            filePanel.Children.Add(fileDesc);
            filePanel.Children.Add(browseButton);
            filePanel.Children.Add(filePickedText);
            
            filePanel.Visibility = Visibility.Collapsed;

            // 4. Панель для веб-ссылки
            var linkPanel = new StackPanel { Spacing = 8, Visibility = Visibility.Collapsed };
            var linkDesc = new TextBlock
            {
                Width = 303,
                Text = "ThemeBgFromURLWhy".GetLocalized(),
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            };
            var linkTextBox = new TextBox
            {
                Width = 303,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText = "https://..."
            };
            linkPanel.Children.Add(linkDesc);
            linkPanel.Children.Add(linkTextBox);

            // 5. Текст-предупреждение про GIF
            var warningText = new TextBlock
            {
                Width = 303,
                Text = "ThemeBgGifWarn".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };

            // Собираем всё в корень
            rootPanel.Children.Add(modeSelector);
            rootPanel.Children.Add(filePanel);
            rootPanel.Children.Add(linkPanel);
            rootPanel.Children.Add(warningText);

            // Логика переключения панелей
            rbNone.Checked += (_, _) =>
            {
                isFileMode = false;
                filePanel.Visibility = Visibility.Collapsed;
                linkPanel.Visibility = Visibility.Collapsed;
            };
            rbFile.Checked += (_, _) =>
            {
                isFileMode = true;
                filePanel.Visibility = Visibility.Visible;
                linkPanel.Visibility = Visibility.Collapsed;
            };
            rbLink.Checked += (_, _) =>
            {
                isFileMode = false;
                filePanel.Visibility = Visibility.Collapsed;
                linkPanel.Visibility = Visibility.Visible;
            };

            // Логика выбора файла
            browseButton.Click += (_, _) =>
            {
                var ofn = new OpenFileName
                {
                    structSize = Marshal.SizeOf<OpenFileName>(),
                    filter = ".png\0*.png\0.jpeg\0*.jpeg\0.jpg\0*.jpg\0.gif\0*.gif\0",
                    file = new string(new char[256]),
                    fileTitle = new string(new char[64]),
                    title = "Saku Overclock: Open image for theme background...",
                    defExt = "png"
                };
                ofn.maxFile = ofn.file.Length;
                ofn.maxFileTitle = ofn.fileTitle.Length;

                if (OpenFileDialog.GetOpenFileNameApi(ofn))
                {
                    endPath = ofn.file.TrimEnd('\0');
                    filePickedText.Text = "ThemePickedFile".GetLocalized() + endPath;
                    filePickedText.Visibility = Visibility.Visible;
                }
            };

            var dialog = new ContentDialog
            {
                Title = "ThemeBgDialog".GetLocalized(),
                Content = rootPanel,
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "ThemeSelect".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = XamlRoot;

            var result = await dialog.ShowAsync();

            // Берем путь из нужного элемента перед сохранением
            var finalPath = isFileMode ? endPath : linkTextBox.Text;

            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(finalPath))
            {
                if (rbNone.IsChecked == true)
                {
                    _themeSelectorService.Themes[_appSettings.ThemeType].ThemeCustomBg = false;
                }
                else
                {
                    _themeSelectorService.Themes[_appSettings.ThemeType].ThemeCustomBg = true;
                    _themeSelectorService.Themes[_appSettings.ThemeType].ThemeBackground = finalPath;
                }
                _themeSelectorService.SaveThemeInSettings();
                RefreshCardPreview(_appSettings.ThemeType);
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex);
        }
    }

    #endregion

    #region Add / Reset Themes

    private async void AddTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var nameBox = new TextBox
            {
                MaxLength = 16,
                CornerRadius = new CornerRadius(15),
                Width = 280,
                PlaceholderText = "ThemeNewName".GetLocalized()
            };

            var dialog = new ContentDialog
            {
                Title = "ThemeNewName".GetLocalized(),
                Content = nameBox,
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "ThemeDone".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(nameBox.Text))
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text)) return;
                _themeSelectorService.Themes.Add(new ThemeClass { ThemeName = nameBox.Text });
                _themeSelectorService.SaveThemeInSettings();
                dialog.Hide();
                LoadThemeCards();
                SelectTheme(_themeSelectorService.Themes.Count - 1);
            }
        }
        catch (Exception exception)
        {
            await LogHelper.LogError(exception);
        }
    }
    
    private async void RenameTheme_Click(int theme)
    {
        try
        {
            var nameBox = new TextBox
            {
                MaxLength = 16,
                CornerRadius = new CornerRadius(15),
                Width = 280,
                PlaceholderText = "ThemeNewName".GetLocalized(),
                Text = _themeSelectorService.Themes[theme].ThemeName
            };

            var dialog = new ContentDialog
            {
                Title = "ThemeNewName".GetLocalized(),
                Content = nameBox,
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "ThemeDone".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(nameBox.Text))
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text)) return;
                _themeSelectorService.Themes[theme].ThemeName = nameBox.Text;
                _themeSelectorService.SaveThemeInSettings();
                dialog.Hide();
                LoadThemeCards();
                SelectTheme(theme);
            }
        }
        catch (Exception exception)
        {
            await LogHelper.LogError(exception);
        }
    }
    
    private async void DeleteTheme_Click(int theme)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = "ThemeDelete".GetLocalized() + _themeSelectorService.Themes[theme].ThemeName,
                CloseButtonText = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "Delete".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary
            };

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                _themeSelectorService.Themes.RemoveAt(theme);
                _themeSelectorService.SaveThemeInSettings();
                dialog.Hide();
                LoadThemeCards();
                SelectTheme(0);
            }
        }
        catch (Exception exception)
        {
            await LogHelper.LogError(exception);
        }
    }

    private async void ResetAllThemes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var confirm = new ContentDialog
            {
                Title = "Settings_Themes_Reset_All_Title".GetLocalized(),
                Content = "Settings_Themes_Reset_All_Desc".GetLocalized(),
                PrimaryButtonText = "ThemeResetAction/Text".GetLocalized(),
                CloseButtonText = "Cancel".GetLocalized(),
                DefaultButton = ContentDialogButton.Close
            };
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                confirm.XamlRoot = XamlRoot;

            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            var defaults = _localThemeSettings.GetDefaultThemes();
            _themeSelectorService.Themes.Clear();
            foreach (var t in defaults) _themeSelectorService.Themes.Add(t);
            _themeSelectorService.SaveThemeInSettings();

            _appSettings.ThemeType = 0;
            _appSettings.SaveSettings();
            _themeSelectorService.SetThemeAsync(ElementTheme.Default);

            LoadThemeCards();
            UpdateTheme();
        }
        catch (Exception exception)
        {
            await LogHelper.LogError(exception);
        }
    }

    private void ResetCurrentTheme_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckCanEdit()) return;
        var idx = _appSettings.ThemeType;
        var defaults = _localThemeSettings.GetDefaultThemes();

        // Если для этого индекса существует значение по умолчанию, восстанавливает его иначе - пустой шаблон
        var src = idx < defaults.Count ? defaults[idx] : new ThemeClass();

        var t = _themeSelectorService.Themes[idx];
        t.ThemeOpacity = src.ThemeOpacity;
        t.ThemeMaskOpacity = src.ThemeMaskOpacity;
        t.ThemeCustom = src.ThemeCustom;
        t.ThemeCustomBg = src.ThemeCustomBg;
        t.ThemeBackground = src.ThemeBackground;
        t.ThemeLight = src.ThemeLight;

        _themeSelectorService.SaveThemeInSettings();
        RefreshCardPreview(idx);
        RefreshRightPanel();
        UpdateTheme();
    }

    #endregion

    #region Helpers

    /// <summary>
    ///     Обновляет фон/прозрачность конкретной карточки без перестроения всего списка
    /// </summary>
    private void RefreshCardPreview(int index)
    {
        UpdateTheme();
        if (index < 0 || index >= ThemeCardsPanel.Children.Count) return;
        if (ThemeCardsPanel.Children[index] is not Button card) return;
        if (card.Content is not Grid cardGrid) return;

        var theme = _themeSelectorService.Themes[index];

        if (cardGrid.Children[0] is Border bgBorder)
        {
            bgBorder.Opacity = theme.ThemeOpacity;
            if (!string.IsNullOrEmpty(theme.ThemeBackground))
                try
                {
                    bgBorder.Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(theme.ThemeBackground)),
                        Stretch = Stretch.UniformToFill
                    };
                }
                catch
                {
                    bgBorder.Background = null;
                }
            else
                bgBorder.Background = null;
        }

        if (cardGrid.Children[1] is Grid maskGrid)
            maskGrid.Opacity = theme.ThemeMaskOpacity;
    }

    private void PageBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        if (args.Index == 0) _navigationService.GoBack();
    }

    #endregion
}