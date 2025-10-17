using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.ViewModels;
using Windows.UI;
using Windows.UI.Text;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;
using Package = Windows.ApplicationModel.Package;

namespace Saku_Overclock.Services;
public abstract partial class UpdateChecker
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>(); // Настройки приложения
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); // Уведомления приложения
    private static readonly Version CurrentVersion = RuntimeHelper.IsMsix ?
        new Version(Package.Current.Id.Version.Major,
            Package.Current.Id.Version.Minor,
            Package.Current.Id.Version.Build,
            Package.Current.Id.Version.Revision)
        : Assembly.GetExecutingAssembly().GetName().Version!;

    private const string RepoOwner = "Erruar";
    private const string RepoName = "Saku-Overclock";
    private static double _downloadPercent; // Процент скачивания
    private static string _timeElapsed = "0:00"; // Время, прошедшее с начала скачивания
    private static string _timeLeft = "0:01"; // Время, оставшееся до завершения скачивания

    public static string? GitHubInfoString
    {
        get; private set;
    }

    public static string CurrentSubVersion
    {
        get;
    } = ГлавнаяViewModel.GetVersion();

    private static Release? _updateNewVersion;
    private static bool _apiRateLimited;

    public static async Task CheckForUpdates()
    {
        if (_apiRateLimited) { return; }
        var client = new GitHubClient(new ProductHeaderValue("Saku-Overclock-Updater"));
        IReadOnlyList<Release>? releases;
        try
        {
           releases = await client.Repository.Release.GetAll(RepoOwner, RepoName);

        }
        catch
        {
            _apiRateLimited = true;
            return;
        }

        // Выбираем релиз с самой высокой версией
        var latestRelease = releases
            .Select(r => new { Release = r, Version = ParseVersion(r.TagName) })
            .OrderByDescending(r => r.Version)
            .FirstOrDefault();

        if (latestRelease == null)
        {
            await App.MainWindow.ShowMessageDialogAsync("Main_NoReleases".GetLocalized(), "Error".GetLocalized());
            return;
        }
        if (latestRelease.Version > CurrentVersion)
        {
            _updateNewVersion = latestRelease.Release;

            if (AppSettings.CheckForUpdates)
            {
                var navigationService = App.GetService<INavigationService>();
                navigationService.NavigateTo(typeof(ОбновлениеViewModel).FullName!, null, true);
            }
            else 
            {
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "UPDATE_REQUIRED",
                    Msg = "DEBUG MESSAGE",
                    Type = InfoBarSeverity.Informational
                });
                NotificationsService.SaveNotificationsSettings();
            }
        }
    }
    public static Release? GetNewVersion()
    {
        return _updateNewVersion;
    }

    public static async Task GenerateReleaseInfoString()
    {
        if (_apiRateLimited) 
        {
            GitHubInfoString = "**Failed to fetch info**";
            return; 
        }
        try
        {

            var client = new GitHubClient(new ProductHeaderValue("Saku-Overclock-Updater"));
            IReadOnlyList<Release>? releases;
            try
            {
                if (_apiRateLimited)
                {
                    GitHubInfoString = "**Failed to fetch info**";
                    return;
                }
                releases = await client.Repository.Release.GetAll(RepoOwner, RepoName);
            }
            catch (RateLimitExceededException v1)
            {
                _apiRateLimited = true;
                GitHubInfoString = $"**Failed to fetch info**\n{v1}";
                return;
            }
            catch
            {
                _apiRateLimited = true;
                GitHubInfoString = "**Failed to fetch info**";
                return;
            }
            

            var sb = new StringBuilder();
            var currentRelease = 0;
            foreach (var release in releases.OrderByDescending(r => r.CreatedAt))
            {
                if (currentRelease > 3)
                {
                    break;
                }
                sb.AppendLine($"{release.Body}".Replace("### THATS ALL?\r\nDon't think that I'm not developing a project, I'm doing it every day for you friends, but so far I can't make a stable update because there are too many changes, but we're getting close to release!\r\nI hope you will appreciate my work as your **star** ⭐ , thank you!", "") + "\n")
                  .AppendLine();
                currentRelease++;
            }

            GitHubInfoString = sb.ToString();
        }
        catch
        {
            _apiRateLimited = true;
            GitHubInfoString = "**Failed to fetch info**";
        }
    }

    public static Version ParseVersion(string tagName)
    {
        // Пример тега: "Saku-Overclock-1.0.14.0-Release-Candidate-5"
        var versionString = tagName.Split('-')[2];
        return Version.TryParse(versionString, out var version) ? version : new Version(0, 0, 0, 0);
    }
    public static double GetDownloadPercent()
    {
        return _downloadPercent;
    }
    public static string GetDownloadTimeLeft()
    {
        return _timeLeft;
    }
    public static string GetDownloadTimeElapsed()
    {
        return _timeElapsed;
    }
    public static async Task DownloadAndUpdate(Release release, IProgress<(double percent, string elapsed, string left)> progress)
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
            var client = new HttpClient();
            var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L; // Общий размер файла в байтах
            var buffer = new byte[8192];
            var totalRead = 0L;
            var isMoreToRead = true;

            var stopwatch = Stopwatch.StartNew(); // Таймер для отслеживания времени

            var fs = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None);
            var contentStream = await response.Content.ReadAsStreamAsync();
            while (isMoreToRead)
            {
                var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (read == 0)
                {
                    isMoreToRead = false;
                    continue;
                }

                await fs.WriteAsync(buffer, 0, read);
                totalRead += read;

                // Обновление процентов скачивания
                if (totalBytes > 0)
                {
                    _downloadPercent = (double)totalRead / totalBytes * 100;
                }

                // Обновление времени загрузки
                var elapsed = stopwatch.Elapsed;
                _timeElapsed = $"{elapsed.Minutes}:{elapsed.Seconds:D2}";

                // Оценка оставшегося времени
                if (totalRead > 0 && _downloadPercent > 0)
                {
                    var estimatedTotalTime = TimeSpan.FromMilliseconds(elapsed.TotalMilliseconds / _downloadPercent * 100);
                    var remainingTime = estimatedTotalTime - elapsed;
                    _timeLeft = $"{remainingTime.Minutes}:{remainingTime.Seconds:D2}";
                }

                // Сообщаем о прогрессе в UI, если progress не null
                progress?.Report((_downloadPercent, _timeElapsed, _timeLeft));
            }

            await fs.FlushAsync(); // Убедиться, что все данные записаны на диск

            stopwatch.Stop();
            await fs.DisposeAsync();

            // Убедиться, что файл полностью закрыт перед запуском
            if (File.Exists(tempFilePath))
            {
                label_8:
                try
                {
                    // Запуск загруженного установочного файла с правами администратора
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = tempFilePath,
                        Verb = "runas" // Запуск от имени администратора
                    });

                    // Закрытие текущего приложения
                    Application.Current.Exit();
                    App.MainWindow.Close();
                }
                catch (Exception ex)
                {
                    await App.MainWindow.ShowMessageDialogAsync("Main_ErrorReleaseLoad".GetLocalized() + $"{ex.Message}", "Error".GetLocalized());
                    await Task.Delay(2000);
                    goto label_8; // Повторить задачу открытия автообновления приложения, в случае если возникла ошибка доступа
                }
                
            }
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync("Main_ErrorReleaseLoad".GetLocalized() + $"{ex.Message}", "Error".GetLocalized());
        }
    }


    #region NotesWriter

    public static async Task GenerateFormattedReleaseNotes(StackPanel stackPanel)
    {
        stackPanel.Children.Clear();
        if (string.IsNullOrEmpty(GitHubInfoString))
        {
            await GenerateReleaseInfoString();
        }

        var formattedText = FormatReleaseNotes(GitHubInfoString);
        foreach (var paragraph in formattedText)
        {
            stackPanel.Children.Add(paragraph);
        }
    }

    private static UIElement[] FormatReleaseNotes(string? releaseNotes)
    {
        // Удаление ненужных частей текста
        var cleanedNotes = CleanReleaseNotes(releaseNotes);

        // Применение стилей markdown
        return ApplyMarkdownStyles(cleanedNotes);
    }

    private static string CleanReleaseNotes(string? releaseNotes)
    {
        var lines = releaseNotes?.Split([Environment.NewLine], StringSplitOptions.None);
        var cleanedLines = new List<string>();
        for (var i = 0; i < lines?.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith("Highlights:"))
            {
                cleanedLines.Add(line); // Добавляем строку Highlights: 
                i++;
                while (i < lines.Length)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]) || char.IsDigit(lines[i][0]))
                    {
                        cleanedLines.Add(lines[i]);
                    }
                    else
                    {
                        break; // Удаляем всё после строки, которая не начинается с цифры или пустая
                    }

                    i++;
                }

                i--; // Вернемся на шаг назад, чтобы правильно обработать следующую строку
            }
            else
            {
                cleanedLines.Add(line);
            }
        }

        return string.Join(Environment.NewLine, cleanedLines);
    }

    public static UIElement[] ApplyMarkdownStyles(string cleanedNotes)
    {
        var lines = cleanedNotes.Split(["\r\n", "\n"], StringSplitOptions.None);
        var elements = new List<UIElement>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();

            if (trimmedLine.StartsWith("### "))
            {
                var text = trimmedLine[4..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(600),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN
                };
                elements.Add(textBlock);
            }
            else if (trimmedLine.StartsWith("## "))
            {
                var text = trimmedLine[3..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(700),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN
                };
                elements.Add(textBlock);
            }
            else if (trimmedLine.StartsWith("# "))
            {
                var text = trimmedLine[2..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(800),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN
                };
                elements.Add(textBlock);
            }
            else if (trimmedLine.StartsWith("> "))
            {
            }
            else if (trimmedLine.StartsWith("![image]("))
            {
                var text = trimmedLine.Replace("![image](", "").Replace(")", "");
                var spoilerText = new TextBlock
                {
                    Text = "+ Spoiler",
                    FontWeight = new FontWeight(500),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                var spoilerImage = new Image
                {
                    Source = new BitmapImage(new Uri(text)),
                    Visibility = Visibility.Collapsed
                };
                var spoilerButton = new Button
                {
                    BorderBrush = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                    Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Content = new StackPanel
                    {
                        Children =
                        {
                            spoilerText,
                            spoilerImage
                        }
                    }
                };
                spoilerButton.Click += (_, _) =>
                {
                    spoilerImage.Visibility = spoilerImage.Visibility == Visibility.Collapsed
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                    spoilerText.Text = spoilerText.Text.Contains('-') ? "+ Spoiler" : "- Spoiler";
                };
                elements.Add(spoilerButton);
            }
            else
            {
                var matches = UnmanagementWords().Matches(trimmedLine);
                var lastPos = 0;

                foreach (Match match in matches)
                {
                    if (match.Index > lastPos)
                    {
                        var beforeText = trimmedLine[lastPos..match.Index];
                        elements.Add(new TextBlock
                        {
                            Text = beforeText,
                            TextWrapping = TextWrapping.Wrap,
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        });
                    }

                    var highlightedText = match.Groups[1].Value;
                    elements.Add(new TextBlock
                    {
                        Text = highlightedText,
                        FontWeight = new FontWeight(700),
                        Foreground = (Brush)Application.Current.Resources["AccentColor"],
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    });

                    lastPos = match.Index + match.Length;
                }

                if (lastPos < trimmedLine.Length)
                {
                    var remainingText = trimmedLine[lastPos..];
                    elements.Add(new TextBlock
                    {
                        Text = remainingText,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    });
                }
            }
        }

        return [.. elements];
    }

    [GeneratedRegex(@"\*\*(.*?)\*\*")]
    private static partial Regex UnmanagementWords();

    #endregion

}