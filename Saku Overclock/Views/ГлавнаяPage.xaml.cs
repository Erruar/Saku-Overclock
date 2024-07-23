using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.SMUEngine;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml;
using System.Text.RegularExpressions;

namespace Saku_Overclock.Views;

public sealed partial class ГлавнаяPage : Page
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
        MainChangelogStackPanel.Children.Clear();
        await UpdateChecker.GenerateReleaseInfoString();
        await GenerateFormattedReleaseNotes(MainChangelogStackPanel);
        //MainChangelogStackPanel.Children.Add(new TextBlock { Text = UpdateChecker.GitHubInfoString, TextWrapping = Microsoft.UI.Xaml.TextWrapping.WrapWholeWords, Width = 274, Foreground = (Brush)Application.Current.Resources["AccentColor"] });
    }
    #endregion
    #region Event Handlers

    private void Discrd_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://discord.com/invite/yVsKxqAaa7") { UseShellExecute = true });
    }

    private void Preset_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПресетыViewModel).FullName!);
    }

    private void Param_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ПараметрыViewModel).FullName!, null, true);
    }

    private void Info_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        var navigationService = App.GetService<INavigationService>();
        navigationService.NavigateTo(typeof(ИнформацияViewModel).FullName!);
    }
    private void MainGithubReadmeButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock") { UseShellExecute = true });
    }

    private void MainGithubIssuesButton_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/Erruar/Saku-Overclock/issues") { UseShellExecute = true });
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

    private static UIElement[] FormatReleaseNotes(string releaseNotes)
    {
        var lines = releaseNotes.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
        var elements = new List<UIElement>();
        var isHighlightsSection = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];

            if (line.StartsWith("### "))
            {
                var text = line.Substring(4);
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new Windows.UI.Text.FontWeight(500),
                    TextWrapping = TextWrapping.Wrap,
                    Width = 274
                };
                elements.Add(textBlock);
            }
            else if (line.StartsWith("## "))
            {
                var text = line.Substring(3);
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new Windows.UI.Text.FontWeight(600),
                    TextWrapping = TextWrapping.Wrap,
                    Width = 274
                };
                elements.Add(textBlock);
            }
            else if (line.StartsWith("# "))
            {
                var text = line.Substring(2);
                var textBlock = new TextBlock
                {
                    Text = text,
                    FontWeight = new Windows.UI.Text.FontWeight(700),
                    TextWrapping = TextWrapping.Wrap,
                    Width = 274
                };
                elements.Add(textBlock);
            }
            else if (line.StartsWith("> "))
            {
                continue; // Пропускаем строку с цитатой
            }
            else if (line.StartsWith("Highlights:"))
            {
                isHighlightsSection = true;

                elements.Add(new TextBlock
                {
                    Text = "Highlights:",
                    FontWeight = new Windows.UI.Text.FontWeight(700),
                    TextWrapping = TextWrapping.Wrap,
                    Width = 274
                });

                i++;
                while (i < lines.Length)
                {
                    if (string.IsNullOrWhiteSpace(lines[i]))
                    {
                        elements.Add(new TextBlock
                        {
                            Text = lines[i],
                            TextWrapping = TextWrapping.Wrap,
                            Width = 274
                        });
                    }
                    else if (char.IsDigit(lines[i][0]))
                    {
                        elements.Add(new TextBlock
                        {
                            Text = lines[i],
                            TextWrapping = TextWrapping.Wrap,
                            Width = 274
                        });
                    }
                    else
                    {
                        // Удаляем всё после строки, которая не начинается с цифры
                        break;
                    }
                    i++;
                }

                isHighlightsSection = false;
                i--; // Вернемся на шаг назад, чтобы правильно обработать следующую строку
            }
            else
            {
                if (!isHighlightsSection)
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
                                Width = 274
                            });
                        }

                        var highlightedText = match.Groups[1].Value;
                        elements.Add(new TextBlock
                        {
                            Text = highlightedText,
                            FontWeight = new Windows.UI.Text.FontWeight(700),
                            Foreground = (Brush)Application.Current.Resources["AccentColor"],
                            TextWrapping = TextWrapping.Wrap,
                            Width = 274
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
                            Width = 274
                        });
                    }
                }
            }
        }

        return [.. elements];
    }

    [GeneratedRegex("\\*\\*(.*?)\\*\\*")]
    private static partial Regex UnmanagementWords();
    #endregion
}
