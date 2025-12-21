using CommunityToolkit.Mvvm.ComponentModel;
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
        private set => SetProperty(ref _isBackEnabled, value);
    }

    private object? _selectedPage;
    public object? SelectedPage
    {
        get => _selectedPage;
        private set => SetProperty(ref _selectedPage, value);
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
        NavigationService = navigationService;
        NavigationService.Navigated += OnNavigated;
        NavigationViewService = navigationViewService;
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        IsBackEnabled = NavigationService.CanGoBack;

        if (e.SourcePageType == typeof(SettingsPage))
        {
            SelectedPage = NavigationViewService.SettingsItem;
            return;
        }
        var selectedItem = NavigationViewService.GetSelectedItem(e.SourcePageType);
        if (selectedItem != null)
        {
            SelectedPage = selectedItem;
        }
    }
}
