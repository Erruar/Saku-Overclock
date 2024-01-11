using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Services;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage : Page
{
    public SettingsPage SettingsPage
    {
    get; set; }

    public ГлавнаяViewModel ViewModel
    {
        get;
    }
#pragma warning disable IDE0044 // Добавить модификатор только для чтения
    private Config config = new();
#pragma warning restore IDE0044 // Добавить модификатор только для чтения
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
    public ГлавнаяPage()
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
    {
        ViewModel = App.GetService<ГлавнаяViewModel>();
        InitializeComponent();
        config.fanex = false;
        config.tempex = false;
        //ConfigSave();
    }

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
}
