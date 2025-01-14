﻿using System.Diagnostics;
using System.Text.RegularExpressions;
using Windows.UI;
using Windows.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage
{
    public ГлавнаяViewModel ViewModel
    {
        get;
    }

    public ГлавнаяPage()
    {
        ViewModel = App.GetService<ГлавнаяViewModel>();
        InitializeComponent();
        GetUpdates();
    }

    #region Updater

    private async void GetUpdates()
    {
        try
        {
            MainChangelogStackPanel.Children.Clear();
            if (UpdateChecker.GitHubInfoString == string.Empty)
            {
                await UpdateChecker.GenerateReleaseInfoString();
            }

            await GenerateFormattedReleaseNotes(MainChangelogStackPanel);
            //MainChangelogStackPanel.Children.Add(new TextBlock { Text = UpdateChecker.GitHubInfoString, TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords, Width = 274, Foreground = (Brush)Application.Current.Resources["AccentColor"] });
        }
        catch (Exception e)
        {
            SendSmuCommand.TraceIt_TraceError(e.ToString());
        }
    }

    #endregion

    #region Event Handlers

    private void HyperLink_Click(object sender, RoutedEventArgs e)
    {
        var link = "https://github.com/Erruar/Saku-Overclock/wiki/FAQ";
        if (sender is Button { Tag: string str1 })
        {
            link = str1;
        }

        Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
    }

    private void Discrd_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://discord.com/invite/yVsKxqAaa7") { UseShellExecute = true });
    }

    private void Param_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!, null, true);
    }

    private void Info_Click(object sender, RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
    }

    private void MainGithubReadmeButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock") { UseShellExecute = true });
    }

    private void MainGithubIssuesButton_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(
            new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/issues") { UseShellExecute = true });
    }

    #endregion

    #region NotesWriter

    public static async Task GenerateFormattedReleaseNotes(StackPanel stackPanel)
    {
        stackPanel.Children.Clear();
        await UpdateChecker.GenerateReleaseInfoString();
        var formattedText = FormatReleaseNotes(UpdateChecker.GitHubInfoString);
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
        var formattedElements = ApplyMarkdownStyles(cleanedNotes);
        return formattedElements;
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
        //var lines = cleanedNotes.Split([Environment.NewLine], StringSplitOptions.None);
        var lines = cleanedNotes.Split(["\r\n", "\n"], StringSplitOptions.None);
        var elements = new List<UIElement>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimStart(); // Убираем пробелы в начале строки 

            if (line.StartsWith("### "))
            {
                var text = line[4..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(600),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN // Убедитесь, что ширина адаптивная
                };
                elements.Add(textBlock);
            }
            else if (line.StartsWith("## "))
            {
                var text = line[3..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(700),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN // Убедитесь, что ширина адаптивная
                };
                elements.Add(textBlock);
            }
            else if (line.StartsWith("# "))
            {
                var text = line[2..];
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new FontWeight(800),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Width = double.NaN // Убедитесь, что ширина адаптивная
                };
                elements.Add(textBlock);
            }
            else if (line.StartsWith("> "))
            {
            }
            else if (line.StartsWith("![image]("))
            {
                var text = line.Replace("![image](", "").Replace(")", "");
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
                var matches = UnmanagementWords().Matches(line);
                var lastPos = 0;

                foreach (Match match in matches)
                {
                    if (match.Index > lastPos)
                    {
                        var beforeText = line[lastPos..match.Index];
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

                if (lastPos < line.Length)
                {
                    var remainingText = line[lastPos..];
                    elements.Add(new TextBlock
                    {
                        Text = remainingText,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    });
                }
            }
        }

        return [..elements];
    }

    [GeneratedRegex(@"\*\*(.*?)\*\*")]
    private static partial Regex UnmanagementWords();

    #endregion
}