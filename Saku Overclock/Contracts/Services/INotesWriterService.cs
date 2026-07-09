using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Contracts.Services;

public interface INotesWriterService
{
    /// <summary>
    ///     Create application update notes (run only in UI-thread)
    /// </summary>
    /// <param name="stackPanel">StackPanel to place notes</param>
    /// <returns>Task result</returns>
    Task GenerateFormattedReleaseNotes(StackPanel stackPanel);

    /// <summary>
    ///     Format MD-text as RichTextBlock elements (run only in UI-thread)
    /// </summary>
    /// <param name="releaseNotes">MD-text</param>
    /// <returns>RichTextBlock with formatted MD-text</returns>
    RichTextBlock FormatReleaseNotesAsRichText(string? releaseNotes);

    /// <summary>
    ///     Update colors for correct element displaying
    /// </summary>
    /// <param name="accent">Accent color</param>
    /// <param name="secondary">Secondary text color</param>
    /// <param name="strong">Strong text color</param>
    void UpdateReleaseNotesBrushes(Brush accent, Brush secondary, Brush strong);
}