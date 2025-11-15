using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Windows.UI.Text;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Octokit;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.ViewModels;
using Application = Microsoft.UI.Xaml.Application;
using FileMode = System.IO.FileMode;
using Package = Windows.ApplicationModel.Package;

namespace Saku_Overclock.Services;

public abstract partial class UpdateChecker
{
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();

    private static readonly Version CurrentVersion = RuntimeHelper.IsMsix
        ? new Version(Package.Current.Id.Version.Major,
            Package.Current.Id.Version.Minor,
            Package.Current.Id.Version.Build,
            Package.Current.Id.Version.Revision)
        : Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 19, 0);

    private const string RepoOwner = "Erruar";
    private const string RepoName = "Saku-Overclock";

    private static Brush _strongFill = (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"];
    private static Brush _secondaryFill = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    private static Brush _accentFill = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];

    public static string? GitHubInfoString
    {
        get;
        private set;
    }

    private static Release? _updateNewVersion;
    private static bool _apiRateLimited;

    #region Updater

    /// <summary>
    ///     Проверяет наличие обновлений программы, при наличии переключает активную страницу на страницу обновления
    /// </summary>
    public static async Task CheckForUpdates()
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
                NotifyUpdate();
            }
        }
    }

    /// <summary>
    ///     Переключает активную страницу на страницу обновления если включено автообновление программы, иначе показывает
    ///     уведомление о наличии обновления
    /// </summary>
    private static void NotifyUpdate()
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

    /// <summary>
    ///     Возвращает релиз новой версии (включая название, когда он был опубликован, файлы, и т.д)
    /// </summary>
    public static Release? GetNewVersion() => _updateNewVersion;

    /// <summary>
    ///     Получает строку информации о последних трёх релизах программы
    /// </summary>
    public static async Task GenerateReleaseInfoString()
    {
        try
        {
            var client = new GitHubClient(new ProductHeaderValue("Saku-Overclock-Updater"));
            var releases = await client.Repository.Release.GetAll(RepoOwner, RepoName);

            var sb = new StringBuilder();
            const string removeText =
                "### THATS ALL?\r\nDon't think that I'm not developing a project, I'm doing it every day for you friends, but so far I can't make a stable update because there are too many changes, but we're getting close to release!\r\nI hope you will appreciate my work as your **star** ⭐ , thank you!";

            foreach (var release in releases.OrderByDescending(r => r.CreatedAt).Take(4))
            {
                sb.AppendLine(release.Body?.Replace(removeText, "") ?? string.Empty)
                    .AppendLine();
            }

            GitHubInfoString = sb.ToString();
        }
        catch
        {
            _apiRateLimited = true;
            GitHubInfoString = "**Failed to fetch info**";
        }
    }

    /// <summary>
    ///     Парсит версию программы по тегу релиза
    /// </summary>
    /// <returns>Версия программы</returns>
    public static Version ParseVersion(string tagName)
    {
        // Пример тега: "Saku-Overclock-1.0.14.0-Release-Candidate-5"
        var parts = tagName.Split('-');
        if (parts.Length > 2 && Version.TryParse(parts[2], out var version))
        {
            return version;
        }

        return new Version(0, 0, 0, 0);
    }

    /// <summary>
    ///     Скачивает новый релиз и возвращает текущий прогресс его загрузки (процент скачивания, оставшееся и прошедшее время)
    /// </summary>
    public static async Task DownloadAndUpdate(Release release,
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
            // Скачивание файла в отдельном scope для гарантированного освобождения ресурсов
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

                        // Обновление процентов скачивания
                        var downloadPercent = totalBytes > 0 ? (double)totalRead / totalBytes * 100 : 0;

                        // Обновление времени загрузки
                        var elapsed = stopwatch.Elapsed;
                        var timeElapsed = $"{elapsed.Minutes}:{elapsed.Seconds:D2}";

                        // Оценка оставшегося времени
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
                } // Гарантированное закрытие потоков здесь

                stopwatch.Stop();
            }

            // Дополнительная задержка для полного освобождения файла системой
            await Task.Delay(500);

            // Запуск установщика после полного освобождения файла
            await LaunchInstallerWithRetry(tempFilePath);
        }
        catch (Exception ex)
        {
            await App.MainWindow.ShowMessageDialogAsync("Main_ErrorReleaseLoad".GetLocalized() + $"{ex.Message}",
                "Error".GetLocalized());
        }
    }

    /// <summary>
    ///     Запускает установку нового релиза, пропует перезапустить при ошибке
    /// </summary>
    private static async Task LaunchInstallerWithRetry(string filePath)
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

                // Проверка доступности файла
                await using (File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Файл доступен
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    Verb = "runas",
                    UseShellExecute = true
                });

                Application.Current.Exit();
                App.MainWindow.Close();
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

    #endregion

    #region NotesWriter

    public static async Task GenerateFormattedReleaseNotes(StackPanel stackPanel)
    {
        stackPanel.Children.Clear();
        if (string.IsNullOrEmpty(GitHubInfoString))
        {
            await GenerateReleaseInfoString();
        }

        var richTextBlock = FormatReleaseNotesAsRichText(GitHubInfoString);
        stackPanel.Children.Add(richTextBlock);
    }

    public static RichTextBlock FormatReleaseNotesAsRichText(string? releaseNotes)
    {
        var cleanedNotes = CleanReleaseNotes(releaseNotes);
        var richTextBlock = new RichTextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0)
        };

        ParseAndAddContent(cleanedNotes, richTextBlock);
        return richTextBlock;
    }

    public static void UpdateReleaseNotesBrushes(Brush accent, Brush secondary, Brush strong)
    {
        _accentFill = accent;
        _secondaryFill = secondary;
        _strongFill = strong;
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
                cleanedLines.Add(line);
                i++;
                while (i < lines.Length)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]) || char.IsDigit(lines[i][0]))
                    {
                        cleanedLines.Add(lines[i]);
                    }
                    else
                    {
                        break;
                    }

                    i++;
                }

                i--;
            }
            else
            {
                cleanedLines.Add(line);
            }
        }

        return string.Join(Environment.NewLine, cleanedLines);
    }

    private static void ParseAndAddContent(string cleanedNotes, RichTextBlock richTextBlock)
    {
        var lines = cleanedNotes.Split(["\r\n", "\n"], StringSplitOptions.None);
        var previousWasEmpty = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimStart();

            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // Пустая строка - отмечаем, но не добавляем сразу
                previousWasEmpty = true;
                continue;
            }

            // Если предыдущая строка была пустой, добавляем небольшой отступ
            var topMargin = previousWasEmpty ? 4.0 : 0.0;
            previousWasEmpty = false;

            if (trimmedLine.StartsWith("### "))
            {
                AddHeading(richTextBlock, trimmedLine[4..], 18, 600, topMargin);
            }
            else if (trimmedLine.StartsWith("## "))
            {
                AddHeading(richTextBlock, trimmedLine[3..], 18, 700, topMargin);
            }
            else if (trimmedLine.StartsWith("# "))
            {
                AddHeading(richTextBlock, trimmedLine[2..], 20, 800, topMargin);
            }
            else if (trimmedLine.StartsWith("> "))
            {
                AddBlockquote(richTextBlock, trimmedLine[2..], topMargin);
            }
            else if (trimmedLine.StartsWith("![image]("))
            {
                AddImageSpoiler(richTextBlock, trimmedLine, topMargin);
            }
            else if (ContainsUrl(trimmedLine))
            {
                AddParagraphWithLinks(richTextBlock, trimmedLine, topMargin);
            }
            else
            {
                AddFormattedParagraph(richTextBlock, trimmedLine, topMargin);
            }
        }
    }

    private static void AddHeading(RichTextBlock richTextBlock, string text, double fontSize, int fontWeight,
        double topMargin = 0)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, topMargin + 6, 0, 0),
            FontSize = fontSize,
            FontWeight = new FontWeight((ushort)fontWeight),
            LineHeight = 1
        };
        paragraph.Inlines.Add(new Run { Text = text });
        richTextBlock.Blocks.Add(paragraph);
    }

    private static void AddBlockquote(RichTextBlock richTextBlock, string text, double topMargin = 0)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(12, topMargin, 0, 0),
            FontStyle = FontStyle.Italic,
            LineHeight = 1
        };

        // Добавляем вертикальную линию через Border (нужен InlineUIContainer)
        var border = new Border
        {
            Margin = new Thickness(-10, 0, 0, 0),
            BorderBrush = _strongFill,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = text,
                TextWrapping = TextWrapping.Wrap,
                FontStyle = FontStyle.Italic,
                Foreground = _secondaryFill
            }
        };

        var container = new InlineUIContainer
        {
            Child = border
        };

        paragraph.Inlines.Add(container);
        richTextBlock.Blocks.Add(paragraph);
    }

    private static void AddImageSpoiler(RichTextBlock richTextBlock, string line, double topMargin = 0)
    {
        var imageUrl = line.Replace("![image](", "").Replace(")", "").Trim();

        var spoilerText = new TextBlock
        {
            Text = "+ Spoiler",
            FontWeight = FontWeights.Medium,
            Margin = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var spoilerImage = new Image
        {
            Source = new BitmapImage(new Uri(imageUrl)),
            Visibility = Visibility.Collapsed,
            Stretch = Stretch.Uniform,
            MaxHeight = 400,
            Margin = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var spoilerStack = new StackPanel
        {
            Children = { spoilerText, spoilerImage },
            HorizontalAlignment = HorizontalAlignment.Stretch
        };

        var spoilerButton = new Button
        {
            BorderBrush = new SolidColorBrush(Colors.Transparent),
            Background = new SolidColorBrush(Colors.Transparent),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Content = spoilerStack,
            Padding = new Thickness(0),
            Margin = new Thickness(0)
        };

        spoilerButton.Click += (_, _) =>
        {
            spoilerImage.Visibility = spoilerImage.Visibility == Visibility.Collapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
            spoilerText.Text = spoilerText.Text.Contains('-') ? "+ Spoiler" : "- Spoiler";
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, topMargin, 0, 0),
            LineHeight = 1
        };
        var container = new InlineUIContainer
        {
            Child = spoilerButton
        };

        // Hack: устанавливаем ширину контейнера равной ширине RichTextBlock
        richTextBlock.SizeChanged += (s, _) =>
        {
            if (s is RichTextBlock rtb)
            {
                spoilerButton.Width = rtb.ActualWidth;
            }
        };

        paragraph.Inlines.Add(container);
        richTextBlock.Blocks.Add(paragraph);
    }

    private static bool ContainsUrl(string text) => text.Contains("https://") || text.Contains("http://");

    private static void AddParagraphWithLinks(RichTextBlock richTextBlock, string text, double topMargin = 0)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, topMargin, 0, 0) };

        // Простой парсинг URL в тексте
        var urlPattern = @"(https?://[^\s]+)";
        var matches = Regex.Matches(text, urlPattern);

        if (matches.Count == 0)
        {
            // Нет ссылок, добавляем как обычный текст
            AddFormattedParagraph(richTextBlock, text);
            return;
        }

        var lastPos = 0;
        foreach (Match match in matches)
        {
            // Текст до ссылки
            if (match.Index > lastPos)
            {
                var beforeText = text[lastPos..match.Index];
                AddInlineFormatting(paragraph, beforeText);
            }

            // Сама ссылка
            var url = match.Value;
            var hyperlink = new Hyperlink
            {
                NavigateUri = new Uri(url)
            };
            hyperlink.Inlines.Add(new Run { Text = url });
            paragraph.Inlines.Add(hyperlink);

            lastPos = match.Index + match.Length;
        }

        // Текст после последней ссылки
        if (lastPos < text.Length)
        {
            var remainingText = text[lastPos..];
            AddInlineFormatting(paragraph, remainingText);
        }

        richTextBlock.Blocks.Add(paragraph);
    }

    private static void AddInlineFormatting(Paragraph paragraph, string text)
    {
        var matches = BoldTextRegex().Matches(text);
        var lastPos = 0;

        foreach (Match match in matches)
        {
            if (match.Index > lastPos)
            {
                var beforeText = text[lastPos..match.Index];
                paragraph.Inlines.Add(new Run { Text = beforeText });
            }

            var boldText = match.Groups[1].Value;
            var boldRun = new Run
            {
                Text = boldText,
                FontWeight = FontWeights.Bold,
                Foreground = _accentFill
            };
            paragraph.Inlines.Add(boldRun);

            lastPos = match.Index + match.Length;
        }

        if (lastPos < text.Length)
        {
            var remainingText = text[lastPos..];
            paragraph.Inlines.Add(new Run { Text = remainingText });
        }
    }

    private static void AddFormattedParagraph(RichTextBlock richTextBlock, string text, double topMargin = 0)
    {
        var paragraph = new Paragraph { Margin = new Thickness(0, topMargin, 0, 0) };

        var matches = BoldTextRegex().Matches(text);
        var lastPos = 0;

        foreach (Match match in matches)
        {
            // Добавляем текст до жирного
            if (match.Index > lastPos)
            {
                var beforeText = text[lastPos..match.Index];
                paragraph.Inlines.Add(new Run { Text = beforeText });
            }

            // Добавляем жирный текст
            var boldText = match.Groups[1].Value;
            var boldRun = new Run
            {
                Text = boldText,
                FontWeight = FontWeights.Bold,
                Foreground = _accentFill
            };
            paragraph.Inlines.Add(boldRun);

            lastPos = match.Index + match.Length;
        }

        // Добавляем оставшийся текст
        if (lastPos < text.Length)
        {
            var remainingText = text[lastPos..];
            paragraph.Inlines.Add(new Run { Text = remainingText });
        }

        richTextBlock.Blocks.Add(paragraph);
    }

    [GeneratedRegex(@"\*\*(.*?)\*\*")]
    private static partial Regex BoldTextRegex();

    #endregion
}