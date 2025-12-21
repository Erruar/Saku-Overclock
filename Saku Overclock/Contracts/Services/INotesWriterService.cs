using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Contracts.Services;
public interface INotesWriterService
{
    Task GenerateFormattedReleaseNotes(StackPanel stackPanel);
    RichTextBlock FormatReleaseNotesAsRichText(string? releaseNotes);
    void UpdateReleaseNotesBrushes(Brush accent, Brush secondary, Brush strong);
}