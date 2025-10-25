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
        HideNavigationBar();

        Loaded += (_, _) =>
        {
            GetUpdates();
        };
        Unloaded += (_, _) =>
        {
            HideNavigationBar();
        };
    }

    #region Updater

    /// <summary>
    ///     Проверить наличие обновлений, обновить информацию в интерфейсе
    /// </summary>
    private async void GetUpdates()
    {
        try
        {
            if (_newVersion == null)
            {
                await UpdateChecker.CheckForUpdates();
                _newVersion = UpdateChecker.GetNewVersion();
            }

            UpdateNewTime.Text = _newVersion?.PublishedAt!.Value
                .UtcDateTime
                .ToLocalTime()
                .ToString("f", CultureInfo.CurrentUICulture);
            UpdateNewVer.Text = UpdateChecker.ParseVersion(_newVersion?.TagName ?? "").ToString();
            MainChangelogContent.Children.Clear();
            if (UpdateChecker.GitHubInfoString == string.Empty)
            {
                await UpdateChecker.GenerateReleaseInfoString();
            }

            UpdateChecker.UpdateReleaseNotesBrushes(AccentTextBrush.Background,
                TextSecondaryBrush.Background,
                ControlStrongBrush.Background);
            await UpdateChecker.GenerateFormattedReleaseNotes(MainChangelogContent);
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    /// <summary>
    ///     Изменяет состояние NavigationBar
    /// </summary>
    private static void HideNavigationBar()
    {
        NotificationsService.Notifies ??= [];
        NotificationsService.Notifies.Add(new Notify
            { Title = "UpdateNAVBAR", Msg = "true", Type = InfoBarSeverity.Informational });
        NotificationsService.SaveNotificationsSettings();
    }

    #endregion

    #region Event Handlers

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateButtonGrid.Visibility = Visibility.Collapsed;
            UpdateDownloadingStackPanel.Visibility = Visibility.Visible;
            if (_newVersion == null)
            {
                return;
            }

            // Прогресс для обновления UI
            var progress = new Progress<(double percent, string elapsed, string left)>(value =>
            {
                UpdatePercentBar.Value = value.percent;
                UpdateNewUpdateDownloading.Text = $"{(int)value.percent}%";
                UpdateNewDownloadingReqTime.Text = value.left;
                UpdateNewDownloadingLeftTime.Text = value.elapsed;
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

    #endregion
}