using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Saku_Overclock.Contracts.Services;

public interface INotesWriterService
{
    /// <summary>
    ///     Создать список изменений программы (выполнять только в UI-потоке)
    /// </summary>
    /// <param name="stackPanel">Элемент куда разместить список изменений</param>
    /// <returns>Результат выполнения задачи</returns>
    Task GenerateFormattedReleaseNotes(StackPanel stackPanel);

    /// <summary>
    ///     Форматировать MD-текст как элементы RichTextBlock (выполнять только в UI-потоке)
    /// </summary>
    /// <param name="releaseNotes">MD-текст</param>
    /// <returns>RichTextBlock с форматированным MD-текстом</returns>
    RichTextBlock FormatReleaseNotesAsRichText(string? releaseNotes);

    /// <summary>
    ///     Обновить кисти для правильного создания элементов
    /// </summary>
    /// <param name="accent">Акцентная кисть</param>
    /// <param name="secondary">Дополнительный цвет текста</param>
    /// <param name="strong">Строгий цвет текста</param>
    void UpdateReleaseNotesBrushes(Brush accent, Brush secondary, Brush strong);
}