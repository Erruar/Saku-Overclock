using System.Diagnostics;
using System.Reflection;
using System.Text;
using Microsoft.UI.Xaml.Controls;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Shared.Contracts;
using Saku_Overclock.ViewModels;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;
using Package = Windows.ApplicationModel.Package;

namespace Saku_Overclock.Services;

public class UpdateCheckerService(
    IAppSettingsService appSettings,
    IAppNotificationService notificationsService)
    : IUpdateCheckerService
{
    private readonly Version _currentVersion = RuntimeHelper.IsMsix
        ? new Version(Package.Current.Id.Version.Major,
            Package.Current.Id.Version.Minor,
            Package.Current.Id.Version.Build,
            Package.Current.Id.Version.Revision)
        : Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 19, 0);

    private const string RepoOwner = "Erruar";
    private const string RepoName = "Saku-Overclock";

    private const string RemoveText =
        "### THATS ALL?\r\nDon't think that I'm not developing a project, I'm doing it every day for you friends, but so far I can't make a stable update because there are too many changes, but we're getting close to release!\r\nI hope you will appreciate my work as your **star** ⭐ , thank you!";

    private string? _githubInfoString;
    private Release? _updateNewVersion;
    private bool _apiRateLimited;

    #region Updater

    /// <summary>
    ///     Checks for program updates and switches active page to update page
    /// </summary>
    public async Task CheckForUpdates()
    {
        if (_apiRateLimited)
        {
            return;
        }

        var client = new GitHubClient(new ProductHeaderValue("Saku-Overclock-Updater"));
        IReadOnlyList<Release>? releases;

        try
        {
            releases = await client.Repository.Release.GetAll(RepoOwner, RepoName);

            var sb = new StringBuilder();


            foreach (var release in releases.OrderByDescending(r => r.CreatedAt).Take(4))
            {
                sb.AppendLine(release.Body?.Replace(RemoveText, "") ?? string.Empty)
                    .AppendLine();
            }

            _githubInfoString = sb.ToString();
        }
        catch
        {
            _apiRateLimited = true;
            _githubInfoString = "**Failed to fetch info**";
            return;
        }

        // Select latest release
        var latestRelease = releases
            .Select(r => new { Release = r, Version = ParseVersion(r.TagName) })
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();

        if (latestRelease == null)
        {
            await App.MainWindow.ShowMessageDialogAsync("Main_NoReleases".GetLocalized(), "Error".GetLocalized());
            return;
        }

        if (latestRelease.Version > _currentVersion)
        {
            _updateNewVersion = latestRelease.Release;

            if (appSettings.CheckForUpdates)
            {
                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(ОбновлениеViewModel).FullName!, null, true);
            }
            else
            {
                NotifyUpdate();
            }
        }
    }

    /// <summary>
    ///     Get new app version release (name, publication time, files, etc.)
    /// </summary>
    public Release? GetNewVersion() => _updateNewVersion;

    /// <summary>
    ///     Get release notes for latest app version from update
    /// </summary>
    public string? GetGithubInfoString() => _githubInfoString;

    /// <summary>
    ///     Parse latest app version from update
    /// </summary>
    /// <returns>New app version</returns>
    public Version ParseVersion()
    {
        return ParseVersion(_updateNewVersion?.TagName ?? "Null-null-0.0.0.0");
    }

    /// <summary>
    ///    Download and run update, showing current progress (download percent, time spent, time left)
    /// </summary>
    public async Task DownloadAndUpdate(Release release,
        IProgress<(double percent, string elapsed, string left)> progress)
    {
        var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe") || a.Name.EndsWith(".msi"));
        if (asset == null)
        {
            await App.MainWindow.ShowMessageDialogAsync("Main_NoReleaseFile".GetLocalized(), "Error".GetLocalized());
            return;
        }

        var downloadUrl = asset.BrowserDownloadUrl;
        var tempFilePath = Path.Combine(Path.GetTempPath(), asset.Name);

        try
        {
            {
                using var client = new HttpClient();
                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];
                var totalRead = 0L;

                var stopwatch = Stopwatch.StartNew();

                await using (var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None,
                                 8192, true))
                await using (var contentStream = await response.Content.ReadAsStreamAsync())
                {
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fs.WriteAsync(buffer.AsMemory(0, bytesRead));
                        totalRead += bytesRead;

                        // Updating download percent
                        var downloadPercent = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0;

                        // Updating time spent
                        var elapsed = stopwatch.Elapsed;
                        var timeElapsed = $"{elapsed.Minutes}:{elapsed.Seconds:D2}";

                        // Updating time left
                        var timeLeft = "0:01";
                        if (totalRead > 0 && downloadPercent > 0)
                        {
                            var estimatedTotalTime =
                                TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds / downloadPercent * 100);
                            var remainingTime = estimatedTotalTime - elapsed;
                            timeLeft = $"{remainingTime.Minutes}:{remainingTime.Seconds:D2}";
                        }

                        progress?.Report((downloadPercent, timeElapsed, timeLeft));
                    }

                    await fs.FlushAsync();
                }

                stopwatch.Stop();
            }

            // Delay between launching to fix incomplete download
            await Task.Delay(500);

            // Launch installer
            await LaunchInstallerWithRetry(tempFilePath);
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync("Main_ErrorReleaseLoad".GetLocalized() + $"{ex.Message}",
                "Error".GetLocalized());
        }
    }

    /// <summary>
    ///     Show update available notification
    /// </summary>
    private void NotifyUpdate()
    {
        notificationsService.ShowNotification("UPDATE_REQUIRED",
            "DEBUG MESSAGE",
            InfoBarSeverity.Informational);
    }

    /// <summary>
    ///     Launch new release installing
    /// </summary>
    private async Task LaunchInstallerWithRetry(string filePath)
    {
        const int maxRetries = 5;

        for (var retryCount = 0; retryCount < maxRetries; retryCount++)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("Installer file not found", filePath);
                }

                // Check for file access
                await using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // File accessible
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "runas",
                    UseShellExecute = true
                });

                Application.Current.Exit();
                return;
            }
            catch (Exception ex)
            {
                if (retryCount >= maxRetries - 1)
                {
                    await App.MainWindow.ShowMessageDialogAsync(
                        "Main_ErrorReleaseLoad".GetLocalized() + $"{ex.Message}",
                        "Error".GetLocalized());
                    return;
                }

                await Task.Delay(2000);
            }
        }
    }

    /// <summary>
    ///    Parse app version by tag
    /// </summary>
    /// <returns>App version</returns>
    private static Version ParseVersion(string tagName)
    {
        // Tag example: "Saku-Overclock-1.0.14.0-Release-Candidate-5"
        var parts = tagName.Split('-');
        if (parts.Length > 2 && Version.TryParse(parts[2], out var version))
        {
            return version;
        }

        return new Version(0, 0, 0, 0);
    }

    #endregion
}