using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Octokit;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.SMUEngine;
public class UpdateChecker
{ 
    private static readonly Version currentVersion = RuntimeHelper.IsMSIX == true ? 
        new(Windows.ApplicationModel.Package.Current.Id.Version.Major, 
            Windows.ApplicationModel.Package.Current.Id.Version.Minor, 
            Windows.ApplicationModel.Package.Current.Id.Version.Build, 
            Windows.ApplicationModel.Package.Current.Id.Version.Revision) 
        : Assembly.GetExecutingAssembly().GetName().Version!;
    private static readonly string currentSubVersion = ГлавнаяViewModel.GetVersion();
    private static readonly string repoOwner = "Erruar";
    private static readonly string repoName = "Saku-Overclock";

    public static string? GitHubInfoString
    {
        get; private set;
    }

    public static string CurrentSubVersion => currentSubVersion;

    public static async Task CheckForUpdates()
    {
        var client = new GitHubClient(new ProductHeaderValue("Saku-Overclock-Updater"));
        var releases = await client.Repository.Release.GetAll(repoOwner, repoName);

        // Выбираем релиз с самой высокой версией
        var latestRelease = releases
            .Select(r => new { Release = r, Version = ParseVersion(r.TagName) })
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();

        if (latestRelease == null)
        {
            MessageBox.Show("Не удалось найти релизы на GitHub.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (latestRelease.Version > currentVersion)
        {
            var result = MessageBox.Show(
                $"Доступна новая версия {latestRelease.Release.TagName}. Хотите обновиться?",
                "Доступно обновление!",
                MessageBoxButtons.YesNo);

            if (result == DialogResult.Yes)
            {
                await DownloadAndUpdate(latestRelease.Release);
            }
        }
    }

    public static async Task GenerateReleaseInfoString()
    {
        var client = new GitHubClient(new ProductHeaderValue("Saku-Overclock-Updater"));
        var releases = await client.Repository.Release.GetAll(repoOwner, repoName);

        var sb = new StringBuilder();
        foreach (var release in releases.OrderByDescending(r => r.CreatedAt))
        {
            sb.AppendLine($"{release.Body}")
              .AppendLine();
        }

        GitHubInfoString = sb.ToString();
    }

    private static Version ParseVersion(string tagName)
    {
        // Пример тега: "Saku-Overclock-1.0.14.0-Release-Candidate-5"
        var versionString = tagName.Split('-')[2];
        return Version.TryParse(versionString, out var version) ? version : new Version(0, 0, 0, 0);
    }

    private static async Task DownloadAndUpdate(Release release)
    {
        var asset = release.Assets.FirstOrDefault(a => a.Name.EndsWith(".exe"));
        if (asset == null)
        {
            MessageBox.Show("Не удалось найти установочный файл в релизе.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var downloadUrl = asset.BrowserDownloadUrl;
        var tempFilePath = Path.Combine(Path.GetTempPath(), asset.Name);

        using (var client = new HttpClient())
        {
            var response = await client.GetAsync(downloadUrl);
            response.EnsureSuccessStatusCode();

            using var fs = new FileStream(tempFilePath, System.IO.FileMode.Create, FileAccess.Write, FileShare.None);
            await response.Content.CopyToAsync(fs);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = tempFilePath,
            Verb = "runas" // Запуск от имени администратора
        });

        App.Current.Exit();
        App.MainWindow.Close(); // Закрываем текущее приложение
        
    }
}