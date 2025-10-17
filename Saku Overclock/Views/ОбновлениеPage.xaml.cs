using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls; 
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Services; 
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ОбновлениеPage
{
    private static Release? _newVersion = UpdateChecker.GetNewVersion();
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();

    public ОбновлениеPage()
    {
        App.GetService<ОбновлениеViewModel>();
        InitializeComponent();
        MainWindow.Remove_ContextMenu_Tray();
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
            { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
        NotificationsService.SaveNotificationsSettings();

        GetUpdates();
        Unloaded += (_, _) =>
        {
            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
                { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
            NotificationsService.SaveNotificationsSettings();
        };
    }

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

            await UpdateChecker.GenerateFormattedReleaseNotes(MainChangelogContent); 
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    #endregion

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Update_Button_Grid.Visibility = Visibility.Collapsed;
            Update_Downloading_Stackpanel.Visibility = Visibility.Visible;
            if (_newVersion == null)
            {
                return;
            }

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
            await LogHelper.TraceIt_TraceError(exception);
        }
    } 
    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ОбучениеPage.ShowNavbarAndControls();
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
    }
}