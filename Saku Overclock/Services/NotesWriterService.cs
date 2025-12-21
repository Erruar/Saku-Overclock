using System.Text.RegularExpressions;
using Windows.UI.Text;
using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Services;

public partial class NotesWriterService(
    IUpdateCheckerService updateChecker)
    : INotesWriterService
{
    private Brush _strongFill = (Brush)Application.Current.Resources["ControlStrongFillColorDefaultBrush"];
    private Brush _secondaryFill = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
    private Brush _accentFill = (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"];

    public async Task GenerateFormattedReleaseNotes(StackPanel stackPanel)
    {
        stackPanel.Children.Clear();
        if (string.IsNullOrEmpty(updateChecker.GetGithubInfoString()))
        {
            await updateChecker.CheckForUpdates();
        }

        var richTextBlock = FormatReleaseNotesAsRichText(updateChecker.GetGithubInfoString());
        stackPanel.Children.Add(richTextBlock);
    }

    public RichTextBlock FormatReleaseNotesAsRichText(string? releaseNotes)
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

    public void UpdateReleaseNotesBrushes(Brush accent, Brush secondary, Brush strong)
    {
        _accentFill = accent;
        _secondaryFill = secondary;
        _strongFill = strong;
    }

    private string CleanReleaseNotes(string? releaseNotes)
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

    private void ParseAndAddContent(string cleanedNotes, RichTextBlock richTextBlock)
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

    private void AddHeading(RichTextBlock richTextBlock, string text, double fontSize, int fontWeight,
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

    private void AddBlockquote(RichTextBlock richTextBlock, string text, double topMargin = 0)
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

    private void AddImageSpoiler(RichTextBlock richTextBlock, string line, double topMargin = 0)
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

    private bool ContainsUrl(string text) => text.Contains("https://") || text.Contains("http://");

    private void AddParagraphWithLinks(RichTextBlock richTextBlock, string text, double topMargin = 0)
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

    private void AddInlineFormatting(Paragraph paragraph, string text)
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

    private void AddFormattedParagraph(RichTextBlock richTextBlock, string text, double topMargin = 0)
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
}