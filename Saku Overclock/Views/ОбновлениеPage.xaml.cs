using System.Diagnostics;
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
        GetUpdates();

        Loaded += (_, _) =>
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
                await UpdateCheckerService.CheckForUpdates();
                _newVersion = UpdateCheckerService.GetNewVersion();
            }

            UpdateVersion.Text = UpdateCheckerService.ParseVersion().ToString();
            MainChangelogContent.Children.Clear();

            if (UpdateCheckerService.IsDev) UpdateType.Text = "dev";
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
    
    private void ShowUpdateFailed()
    {
        UpdateCancel.Visibility = Visibility.Visible;
        UpdatePercentBar.Visibility = Visibility.Collapsed;

        UpdateProceed.IsEnabled = true;
        UpdateProceed.Content = "UpdateRetryButtonSign".GetLocalized();

        UpdateTitle.Text = "UpdateFailedTitle".GetLocalized();
        UpdateSubtitle.Text = "UpdateFailedSubtitle".GetLocalized();

        UpdateIcon.Glyph = "\uE783";
    }

    #endregion

    #region Event Handlers

    private async void Update_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_newVersion == null)
                return;

            UpdateIcon.Glyph = "\uE895";
            UpdateCancel.Visibility = Visibility.Collapsed;
            UpdatePercentBar.Visibility = Visibility.Visible;

            UpdateProceed.IsEnabled = false;
            UpdateProceed.Content = "UpdatePreparingButtonSign".GetLocalized();

            UpdateTitle.Text = "UpdatePreparingTitle".GetLocalized();
            UpdateSubtitle.Text = "UpdatePreparingSubtitle".GetLocalized();

            UpdatePercentBar.Value = 0;

            var progress = new Progress<(double percent, string elapsed, string left)>(
                value =>
                {
                    UpdatePercentBar.Value = value.percent;
                });

            await UpdateCheckerService.DownloadAndUpdate(_newVersion, progress);
        }
        catch (Exception exception)
        {
            await LogHelper.TraceIt_TraceError(exception);

            ShowUpdateFailed();

            await App.MainWindow.ShowMessageDialogAsync(
                "Main_ErrorReleaseLoad".GetLocalized() + exception.Message,
                "Error".GetLocalized());
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        ОбучениеPage.ShowNavbarAndControls();
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, null, true);
    }
    
    private void UpdateLearnAbout_OnClick(object sender, RoutedEventArgs e) => Process.Start(
        new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/releases") { UseShellExecute = true });

    #endregion
}