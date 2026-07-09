using Octokit;

namespace Saku_Overclock.Contracts.Services;
public interface IUpdateCheckerService
{
    /// <summary>
    ///     Check for new updates
    /// </summary>
    /// <returns>Task result</returns>
    Task CheckForUpdates();
    
    /// <summary>
    ///     Get new app version release
    /// </summary>
    /// <returns>GitHub release</returns>
    Release? GetNewVersion();
    
    /// <summary>
    ///     Get release notes for latest app version from update
    /// </summary>
    /// <returns>Release notes</returns>
    string? GetGithubInfoString();
    
    /// <summary>
    ///     Parse latest app version from update
    /// </summary>
    /// <returns>New app version</returns>
    Version ParseVersion();
    
    /// <summary>
    ///     Download and run update
    /// </summary>
    /// <param name="release">GitHub release</param>
    /// <param name="progress">Update progress</param>
    /// <returns>Task result</returns>
    Task DownloadAndUpdate(Release release, IProgress<(double percent, string elapsed, string left)> progress);
}
