using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage : Page
{ 
    public ГлавнаяViewModel ViewModel
    {
        get;
    } 
    public ГлавнаяPage()
    {
        ViewModel = App.GetService<ГлавнаяViewModel>();
        InitializeComponent(); 
    }
    #region Event Handlers

    private void Discrd_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://discord.com/invite/yVsKxqAaa7") { UseShellExecute = true });
    }

    private void Preset_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
    }

    private void Param_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!);
    }

    private void Info_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
    }
    #endregion
}
