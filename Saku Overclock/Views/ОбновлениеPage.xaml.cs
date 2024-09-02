using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;
using Windows.Foundation;
using Windows.Foundation.Collections; 

namespace Saku_Overclock.Views; 
public sealed partial class ОбновлениеPage : Page
{
    private static JsonContainers.Notifications notify = new(); // Уведомления приложения
    public ОбновлениеViewModel ViewModel 
    { 
        get; 
    }
    public ОбновлениеPage()
    {
        ViewModel = App.GetService<ОбновлениеViewModel>(); 
        InitializeComponent();
        NotifyLoad();
        notify.Notifies ??= [];
        notify.Notifies.Add(new Notify { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
        NotifySave();
        GetUpdates();
        Unloaded += (s, a) => 
        {
            NotifyLoad();
            notify.Notifies ??= [];
            notify.Notifies.Add(new Notify { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
            NotifySave();
        };
    }
    #region JSON
    public static void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify, Formatting.Indented));
        }
        catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
    }
    public static void NotifyLoad()
    {
        var success = false;
        var retryCount = 1;
        while (!success && retryCount < 3)
        {
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))
            {
                try
                {
                    notify = JsonConvert.DeserializeObject<JsonContainers.Notifications>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))!;
                    if (notify != null) { success = true; } 
                }
                catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
            }
            retryCount++;
        }
    }
    #endregion
    #region Updater
    private async void GetUpdates()
    {
        MainChangelogContent.Children.Clear();
        if (UpdateChecker.GitHubInfoString == string.Empty)
        {
            await UpdateChecker.GenerateReleaseInfoString();
        }
        await ГлавнаяPage.GenerateFormattedReleaseNotes(MainChangelogContent);
        //MainChangelogStackPanel.Children.Add(new TextBlock { Text = UpdateChecker.GitHubInfoString, TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords, Width = 274, Foreground = (Brush)Application.Current.Resources["AccentColor"] });
    }
    #endregion
}
