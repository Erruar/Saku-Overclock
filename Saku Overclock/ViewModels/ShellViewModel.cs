using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation; 
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Views;

namespace Saku_Overclock.ViewModels;

public partial class ShellViewModel : ObservableRecipient
{

    private bool _isBackEnabled;
    public bool IsBackEnabled
    {
        get => _isBackEnabled;
        set => SetProperty(ref _isBackEnabled, value);
    }

    private object? _selected;
    public object? Selected
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    private ObservableCollection<ComboBoxItem> _items;
    public ObservableCollection<ComboBoxItem> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    private ComboBoxItem? _selectedItem;
    public ComboBoxItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    private int _selectedIndex;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public INavigationService NavigationService
    {
        get;
    }
    

    public INavigationViewService NavigationViewService
    {
        get;
    }

    public ShellViewModel(INavigationService navigationService, INavigationViewService navigationViewService)
    {
        _items =
        [
            new() {
                Content = new TextBlock 
                { 
                    Text = "User profiles", 
                },
                    IsEnabled = false 
                },
            new() { Content = "dd" },
            new() { Content = "dd1" }
        ];
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
    }  

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;

        if (e.SourcePageType == typeof(SettingsPage))
        {
            Selected = NavigationViewService.SettingsItem;
            return;
        }
        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            Selected = selectedItem;
        }
    }
}
