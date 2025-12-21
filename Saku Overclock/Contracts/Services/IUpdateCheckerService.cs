using Octokit;

namespace Saku_Overclock.Contracts.Services;
public interface IUpdateCheckerService
{
    Task CheckForUpdates();
    Release? GetNewVersion();
    string? GetGithubInfoString();
    Version ParseVersion();
    Task DownloadAndUpdate(Release release, IProgress<(double percent, string elapsed, string left)> progress);
}
