using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Octokit;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views; 
public sealed partial class ОбновлениеPage
{
    private static JsonContainers.Notifications _notify = new(); // Уведомления приложения
    private static Release? _newVersion = UpdateChecker.GetNewVersion();

    public ОбновлениеPage()
    {
        App.GetService<ОбновлениеViewModel>(); 
        InitializeComponent(); 
        MainWindow.Remove_ContextMenu_Tray();
        NotifyLoad();
        _notify.Notifies ??= [];
        _notify.Notifies.Add(new Notify { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
        NotifySave();
        GetUpdates();
        Unloaded += (_, _) => 
        { 
            NotifyLoad();
            _notify.Notifies ??= [];
            _notify.Notifies.Add(new Notify { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
            NotifySave();
        };
    }
    #region JSON

    private static void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(_notify, Formatting.Indented));
        }
        catch (Exception ex) { SendSMUCommand.TraceIt_TraceError(ex.ToString()); }
    }

    private static void NotifyLoad()
    {
        var success = false;
        var retryCount = 1;
        while (!success && retryCount < 3)
        {
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))
            {
                try
                {
                    _notify = JsonConvert.DeserializeObject<JsonContainers.Notifications>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))!;
                    success = true;
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
        try
        {
            if (_newVersion == null)
            {
                await UpdateChecker.CheckForUpdates();
                _newVersion = UpdateChecker.GetNewVersion(); 
            }
            if (_newVersion == null)
            {
                return;
            }
            Update_New_time.Text = _newVersion.PublishedAt!.Value.UtcDateTime.ToString(CultureInfo.InvariantCulture);
            Update_New_ver.Text = UpdateChecker.ParseVersion(_newVersion.TagName).ToString();
            MainChangelogContent.Children.Clear();
            if (UpdateChecker.GitHubInfoString == string.Empty)
            {
                await UpdateChecker.GenerateReleaseInfoString();
            }
            await ГлавнаяPage.GenerateFormattedReleaseNotes(MainChangelogContent);
            //MainChangelogStackPanel.Children.Add(new TextBlock { Text = UpdateChecker.GitHubInfoString, TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords, Width = 274, Foreground = (Brush)Application.Current.Resources["AccentColor"] });
        }
        catch (Exception e)
        {
            SendSMUCommand.TraceIt_TraceError(e.ToString());
        }
    }
    #endregion

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Update_Button_Grid.Visibility = Visibility.Collapsed;
            Update_Downloading_Stackpanel.Visibility = Visibility.Visible;
            if (_newVersion == null) { return; } 
            // Прогресс для обновления UI
            var progress = new Progress<(double percent, string elapsed, string left)>(value =>
            {
                Update_PercentBar.Value = value.percent;
                Update_New_UpdateDownloading.Text = $"{(int)value.percent}%";
                Update_New_Downloading_ReqTime.Text = value.left;
                Update_New_Downloading_LeftTime.Text = value.elapsed;
            });

            await UpdateChecker.DownloadAndUpdate(_newVersion, progress);
        }
        catch (Exception exception)
        {
            SendSMUCommand.TraceIt_TraceError(exception.ToString());
        }
    }
}
