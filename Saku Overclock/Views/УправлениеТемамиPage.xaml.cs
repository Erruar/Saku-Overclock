using System.Numerics;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Styles;

namespace Saku_Overclock.Views;

public sealed partial class УправлениеТемамиPage
{
    // ─── Services ───────────────────────────────────────────────────────────
    private readonly IThemeSelectorService    _themeSelectorService  = App.GetService<IThemeSelectorService>();
    private readonly IAppSettingsService      _appSettings           = App.GetService<IAppSettingsService>();
    private readonly ILocalThemeSettingsService _localThemeSettings  = App.GetService<ILocalThemeSettingsService>();
    private readonly INavigationService       _navigationService     = App.GetService<INavigationService>();
    private readonly IAppNotificationService _notificationsService = App.GetService<IAppNotificationService>(); // Уведомления

    // ─── State ──────────────────────────────────────────────────────────────
    private bool    _isLoading = true;
    private Button? _selectedCardBtn; // reference to currently highlighted card button

    // ────────────────────────────────────────────────────────────────────────
    public УправлениеТемамиPage()
    {
        InitializeComponent();
        SetupBreadcrumb();
        Loaded += OnLoaded;
    }

    // ═══════════════════════════════════════════════════════════════════════
    #region Init

    private void SetupBreadcrumb()
    {
        PageBreadcrumb.ItemsSource = new List<string>
        {
            "Settings_Name/Text".GetLocalized(),   // "Настройки" (локализованная строка)
            "ThemeManager/Text".GetLocalized()      // "Управление темами"
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadThemeCards();
    }

    /// <summary>
    /// Fills the left panel with theme cards and scrolls the active one into view.
    /// </summary>
    private void LoadThemeCards()
    {
        ThemeCardsPanel.Children.Clear();
        _selectedCardBtn = null;

        for (var i = 0; i < _themeSelectorService.Themes.Count; i++)
        {
            var index    = i; // capture for closure
            var theme    = _themeSelectorService.Themes[i];
            var isActive = i == _appSettings.ThemeType;

            // ── build card ────────────────────────────────────────────────
            var maskBorder = new Border
            {
                CornerRadius          = new CornerRadius(12),
                HorizontalAlignment   = HorizontalAlignment.Stretch,
                VerticalAlignment     = VerticalAlignment.Stretch,
                Background = theme.ThemeLight 
                  ? new SolidColorBrush()
                {
                    Color =  Color.FromArgb(255, 249, 249, 249)
                } : new SolidColorBrush()
                {
                    Color = Color.FromArgb(255, 39, 39, 39)
                },
                Opacity = index is > -1 and < 3 ? 0 : 1
            };
            
            var bgBorder = new Border
            {
                CornerRadius          = new CornerRadius(12),
                HorizontalAlignment   = HorizontalAlignment.Stretch,
                VerticalAlignment     = VerticalAlignment.Stretch,
                Opacity               = theme.ThemeOpacity
            };

            // Set background image if path is available
            if (!string.IsNullOrEmpty(theme.ThemeBackground))
            {
                try
                {
                    bgBorder.Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(theme.ThemeBackground)),
                        Stretch     = Stretch.UniformToFill
                    };
                }
                catch { /* no image */ }
            }

            switch (index)
            {
                case 0:
                    bgBorder.Background = new LinearGradientBrush()
                    {
                        StartPoint = new Point(-0.2, -0.3),      // Левый верхний угол
                        EndPoint = new Point(0.6, 2),        // Правый нижний угол
                        GradientStops =
                        {
                            new GradientStop
                            {
                                Color = Color.FromArgb(255, 249, 249, 249),  // Белый
                                Offset = 0.5
                            },
                            new GradientStop
                            {
                                Color = Color.FromArgb(255, 39, 39, 39),        // Черный
                                Offset = 0.5
                            }
                        }
                    };
                    bgBorder.Opacity = 1;
                    break;
                case 1:
                    bgBorder.Background = new SolidColorBrush()
                    {
                        Color = Color.FromArgb(255, 249, 249, 249)
                    };
                    bgBorder.Opacity = 1;
                    break;
                case 2:
                    bgBorder.Background = new SolidColorBrush()
                    {
                        Color = Color.FromArgb(255, 39, 39, 39)
                    };
                    bgBorder.Opacity = 1;
                    break;
            }

            var maskGrid = new Grid
            {
                RequestedTheme      = theme.ThemeLight ? ElementTheme.Light : ElementTheme.Dark,
                CornerRadius        = new CornerRadius(12),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                Opacity             = index is > -1 and < 3 ? 0 : theme.ThemeMaskOpacity,
                Background          = (Brush)Application.Current.Resources["BackgroundImageMaskAcrylicBrush"]
            };

            // Trim name to ≤16 chars for card display
            var displayName = theme.ThemeName.Length > 16
                ? theme.ThemeName[..16] + "…"
                : theme.ThemeName;

            try { displayName = displayName.Contains("Theme_") ? displayName.GetLocalized() : displayName; }
            catch { /* keep as-is */ }

            var nameBtn = new Button
            {
                RequestedTheme      = theme.ThemeLight ? ElementTheme.Light : (index == 0 ? ElementTheme.Light : ElementTheme.Dark),
                Content             = displayName,
                FontSize            = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                CornerRadius        = new CornerRadius(10),
                MaxWidth            = 180,
                Shadow              = SharedShadow,
                Translation         = new Vector3(0, 0, 20),
                IsHitTestVisible    = false  // clicks go to the card, not this button
            };

            var cardGrid = new Grid
            {
                Width  = 196,
                Height = 110
            };
            cardGrid.Children.Add(maskBorder);
            cardGrid.Children.Add(bgBorder);
            cardGrid.Children.Add(maskGrid);
            cardGrid.Children.Add(nameBtn);

            // Outer card button (selection)
            var card = new Button
            {
                Content           = cardGrid,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding           = new Thickness(0),
                CornerRadius      = new CornerRadius(14),
                Margin            = i == 0 ? new Thickness(0) : new Thickness(0, 6, 0, 0),
                BorderThickness   = isActive ? new Thickness(2) : new Thickness(0),
                BorderBrush       = isActive
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : null
            };

            card.Click += (_, _) => SelectTheme(index);

            if (isActive)
            {
                _selectedCardBtn = card;
            }

            ThemeCardsPanel.Children.Add(card);
        }

        // Scroll the active card into view after layout pass
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (_selectedCardBtn != null)
            {
                _selectedCardBtn.StartBringIntoView();
            }
        });

        RefreshRightPanel();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    #region Theme Selection

    /// <summary>
    /// Selects a theme: highlights card, applies theme, refreshes right panel.
    /// </summary>
    private void SelectTheme(int index)
    {
        if (index < 0 || index >= _themeSelectorService.Themes.Count) return;

        // Update border on all cards
        for (var i = 0; i < ThemeCardsPanel.Children.Count; i++)
        {
            if (ThemeCardsPanel.Children[i] is Button btn)
            {
                var isSelected = i == index;
                btn.BorderThickness = isSelected ? new Thickness(2) : new Thickness(0);
                btn.BorderBrush     = isSelected
                    ? (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"]
                    : null;
                if (isSelected) _selectedCardBtn = btn;
            }
        }

        _appSettings.ThemeType = index;
        _appSettings.SaveSettings();

        // Apply theme
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
    /// Updates right-panel visibility and control values for the current theme.
    /// </summary>
    private void RefreshRightPanel()
    {
        var idx = _appSettings.ThemeType;

        // Themes 0–2 → InfoBar, no editing
        var isBuiltIn = idx <= 2;
        BuiltInThemeInfoBar.IsOpen         = isBuiltIn;
        ThemeEditPanel.Visibility          = isBuiltIn ? Visibility.Collapsed : Visibility.Visible;
        NoSelectionText.Visibility         = Visibility.Collapsed;

        if (isBuiltIn || idx >= _themeSelectorService.Themes.Count) return;

        // Load values without triggering saves
        _isLoading = true;

        var t = _themeSelectorService.Themes[idx];
        ThemeOpacitySlider.Value     = t.ThemeOpacity;
        ThemeMaskOpacitySlider.Value = t.ThemeMaskOpacity;
        ThemeCustomBgSwitch.IsOn     = t.ThemeCustomBg;
        ThemeTypeComboBox.SelectedIndex = t.ThemeLight ? 1 : 0;
        
        if (idx > 7)
        {
            ThemeCustomBgSwitch.Visibility = Visibility.Visible;
        }

        _isLoading = false;
    }

    private bool CheckCanEdit() =>
        !_isLoading
        && _appSettings.ThemeType > 2
        && _appSettings.ThemeType < _themeSelectorService.Themes.Count;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    #region Right Panel Event Handlers

    private void ThemeColorIntensity_ValueChanged(object sender, object args)
    {
        if (!CheckCanEdit()) return;
        _themeSelectorService.Themes[_appSettings.ThemeType].ThemeOpacity = ThemeOpacitySlider.Value;
        _themeSelectorService.SaveThemeInSettings();
        // Refresh the card preview
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
        _themeSelectorService.SetThemeAsync(ThemeTypeComboBox.SelectedIndex == 1 ? ElementTheme.Light : ElementTheme.Dark);
    }

    private void ThemeCustomBackground_Toggled(object sender, RoutedEventArgs e)
    {
        if (!CheckCanEdit()) return;
        _themeSelectorService.Themes[_appSettings.ThemeType].ThemeCustomBg = ThemeCustomBgSwitch.IsOn;
        _themeSelectorService.SaveThemeInSettings();
        ThemeBgBtn.Visibility = ThemeCustomBgSwitch.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    // PointerPressed helpers (toggle the linked switch)

    private void ToggleThemeCustomBgSwitch_PointerPressed(object s, PointerRoutedEventArgs e)
        => ThemeCustomBgSwitch.IsOn = !ThemeCustomBgSwitch.IsOn;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    #region Background Picker

    private async void OpenSelectThemeBackground_Click(object sender, object e)
    {
        try
        {
            if (!CheckCanEdit()) return;
            var endPath = "";

            var fromFilePickedText = new TextBlock
            {
                MaxWidth      = 300,
                Visibility    = Visibility.Collapsed,
                TextWrapping  = TextWrapping.WrapWholeWords,
                FontWeight    = new FontWeight(300)
            };
            var fromFile = new Button
            {
                Height              = 90,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius        = new CornerRadius(16),
                Translation         = new Vector3(0, 0, 12),
                Shadow              = SharedShadow,
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin      = new Thickness(12, 0, 0, 0),
                    Children    =
                    {
                        new TextBlock { Text = "ThemeBgFromFile".GetLocalized(), FontWeight = new FontWeight(600) },
                        new TextBlock { Text = "ThemeBgFromFileWhy".GetLocalized(), MaxWidth = 300, FontWeight = new FontWeight(300), TextWrapping = TextWrapping.WrapWholeWords },
                        fromFilePickedText
                    }
                }
            };

            var fromLinkTextBox = new TextBox
            {
                MaxWidth            = 300,
                Visibility          = Visibility.Collapsed,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                PlaceholderText     = "https://...."
            };
            var fromLink = new Button
            {
                Margin              = new Thickness(0, 8, 0, 0),
                Height              = 90,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                CornerRadius        = new CornerRadius(16),
                Translation         = new Vector3(0, 0, 12),
                Shadow              = SharedShadow,
                Content = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Margin      = new Thickness(12, 0, 0, 0),
                    Children    =
                    {
                        new TextBlock { Text = "ThemeBgFromURL".GetLocalized(), FontWeight = new FontWeight(600) },
                        new TextBlock { Text = "ThemeBgFromURLWhy".GetLocalized(), MaxWidth = 300, FontWeight = new FontWeight(300), TextWrapping = TextWrapping.WrapWholeWords },
                        fromLinkTextBox
                    }
                }
            };

            var dialog = new ContentDialog
            {
                Title           = "ThemeBgDialog".GetLocalized(),
                Content         = new StackPanel
                {
                    Orientation         = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Children            = { fromFile, fromLink,
                        new TextBlock
                        {
                            Margin     = new Thickness(0, 6, 0, 0),
                            Text       = "ThemeBgGifWarn".GetLocalized(),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Foreground = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                        }
                    }
                },
                CloseButtonText   = "CancelThis/Text".GetLocalized(),
                PrimaryButtonText = "ThemeSelect".GetLocalized(),
                DefaultButton     = ContentDialogButton.Close
            };

            fromFile.Click += (_, _) =>
            {
                var ofn = new OpenFileName
                {
                    structSize = Marshal.SizeOf(typeof(OpenFileName)),
                    filter     = ".png\0*.png\0.jpeg\0*.jpeg\0.jpg\0*.jpg\0.gif\0*.gif\0",
                    file       = new string(new char[256]),
                    fileTitle  = new string(new char[64]),
                    title      = "Saku Overclock: Open image for theme background...",
                    defExt     = "png"
                };
                ofn.maxFile      = ofn.file.Length;
                ofn.maxFileTitle = ofn.fileTitle.Length;

                if (OpenFileDialog.GetOpenFileNameApi(ofn))
                {
                    endPath                   = ofn.file.TrimEnd('\0');
                    fromFilePickedText.Text    = "ThemePickedFile".GetLocalized() + endPath;
                    fromFilePickedText.Visibility = Visibility.Visible;
                }
            };
            fromLink.Click    += (_, _) => fromLinkTextBox.Visibility =
                fromLinkTextBox.Visibility == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
            fromLinkTextBox.TextChanged += (_, _) => endPath = fromLinkTextBox.Text;

            // ReSharper disable once MultipleResolveCandidatesInText
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = XamlRoot;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(endPath))
            {
                _themeSelectorService.Themes[_appSettings.ThemeType].ThemeBackground = endPath;
                _themeSelectorService.SaveThemeInSettings();
                RefreshCardPreview(_appSettings.ThemeType);
            }
        }
        catch (Exception ex) { await LogHelper.TraceIt_TraceError(ex); }
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    #region Add / Reset Themes

    private async void AddTheme_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var nameBox = new TextBox
            {
                MaxLength       = 16,
                CornerRadius    = new CornerRadius(15, 0, 0, 15),
                Width           = 280,
                PlaceholderText = "ThemeNewName".GetLocalized()
            };
            var confirmBtn = new Button
            {
                CornerRadius = new CornerRadius(0, 15, 15, 0),
                Content      = new FontIcon { Glyph = "\uEC61" }
            };

            var dialog = new ContentDialog
            {
                Title             = "ThemeNewName".GetLocalized(),
                Content           = new StackPanel { Orientation = Orientation.Horizontal, Children = { nameBox, confirmBtn } },
                CloseButtonText   = "CancelThis/Text".GetLocalized(),
                DefaultButton     = ContentDialogButton.Close
            };

            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = XamlRoot;

            confirmBtn.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(nameBox.Text)) return;
                _themeSelectorService.Themes.Add(new ThemeClass { ThemeName = nameBox.Text });
                _themeSelectorService.SaveThemeInSettings();
                dialog.Hide();
                LoadThemeCards();
                // Auto-select the new theme
                SelectTheme(_themeSelectorService.Themes.Count - 1);
            };

            await dialog.ShowAsync();
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
                Title             = "Сбросить все темы?",
                Content           = "Все пользовательские темы будут удалены, стандартные восстановлены.",
                PrimaryButtonText = "Сбросить",
                CloseButtonText   = "Отмена",
                DefaultButton     = ContentDialogButton.Close
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
        var idx      = _appSettings.ThemeType;
        var defaults = _localThemeSettings.GetDefaultThemes();

        // If a default exists at this index, restore it; otherwise use a blank template
        var src = idx < defaults.Count ? defaults[idx] : new ThemeClass();

        var t = _themeSelectorService.Themes[idx];
        t.ThemeOpacity    = src.ThemeOpacity;
        t.ThemeMaskOpacity = src.ThemeMaskOpacity;
        t.ThemeCustom     = src.ThemeCustom;
        t.ThemeCustomBg   = src.ThemeCustomBg;
        t.ThemeBackground = src.ThemeBackground;
        t.ThemeLight      = src.ThemeLight;

        _themeSelectorService.SaveThemeInSettings();
        RefreshCardPreview(idx);
        RefreshRightPanel();
        UpdateTheme();
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════════════
    #region Helpers

    /// <summary>
    /// Refreshes the background/opacity of a specific card without rebuilding the whole list.
    /// </summary>
    private void RefreshCardPreview(int index)
    {
        UpdateTheme();
        if (index < 0 || index >= ThemeCardsPanel.Children.Count) return;
        if (ThemeCardsPanel.Children[index] is not Button card) return;
        if (card.Content is not Grid cardGrid) return;

        var theme = _themeSelectorService.Themes[index];

        // cardGrid children: 0=bgBorder, 1=maskGrid, 2=nameBtn
        if (cardGrid.Children[0] is Border bgBorder)
        {
            bgBorder.Opacity = theme.ThemeOpacity;
            if (!string.IsNullOrEmpty(theme.ThemeBackground))
            {
                try
                {
                    bgBorder.Background = new ImageBrush
                    {
                        ImageSource = new BitmapImage(new Uri(theme.ThemeBackground)),
                        Stretch     = Stretch.UniformToFill
                    };
                }
                catch { bgBorder.Background = null; }
            }
            else { bgBorder.Background = null; }
        }

        if (cardGrid.Children[1] is Grid maskGrid)
            maskGrid.Opacity = theme.ThemeMaskOpacity;
    }

    private void PageBreadcrumb_ItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs args)
    {
        // First item = "Настройки" → go back
        if (args.Index == 0) _navigationService.GoBack();
    }

    #endregion
}