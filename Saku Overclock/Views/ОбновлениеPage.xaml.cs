using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ОбновлениеPage
{
    private static readonly IUpdateCheckerService UpdateCheckerService = App.GetService<IUpdateCheckerService>();
    private static readonly INotesWriterService NotesWriterService = App.GetService<INotesWriterService>();
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();
    private static readonly ITrayMenuService TrayMenuService = App.GetService<ITrayMenuService>();
    private static Release? _newVersion = UpdateCheckerService.GetNewVersion();

    public ОбновлениеPage()
    {
        App.GetService<ОбновлениеViewModel>();
        InitializeComponent();
        TrayMenuService.SetMinimalMode();
        HideNavigationBar();

        Loaded += (_, _) =>
        {
            GetUpdates();
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
                await UpdateCheckerService.CheckForUpdates();
                _newVersion = UpdateCheckerService.GetNewVersion();
            }

            UpdateNewTime.Text = _newVersion?.PublishedAt!.Value
                .UtcDateTime
                .ToLocalTime()
                .ToString("f", CultureInfo.CurrentUICulture);
            UpdateNewVer.Text = UpdateCheckerService.ParseVersion().ToString();
            MainChangelogContent.Children.Clear();
            if (UpdateCheckerService.GetGithubInfoString() == string.Empty)
            {
                await UpdateCheckerService.CheckForUpdates();
            }

            NotesWriterService.UpdateReleaseNotesBrushes(AccentTextBrush.Background,
                TextSecondaryBrush.Background,
                ControlStrongBrush.Background);
            await NotesWriterService.GenerateFormattedReleaseNotes(MainChangelogContent);
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
        NotificationsService.ShowNotification("UpdateNAVBAR",
            "true",
            InfoBarSeverity.Informational);
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

            await UpdateCheckerService.DownloadAndUpdate(_newVersion, progress);
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