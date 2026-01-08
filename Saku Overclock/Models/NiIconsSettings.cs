namespace Saku_Overclock.Models;

internal class NiIconsSettings
{
    /// <summary>
    ///     Полностью пустая коллекция (нет ни одного элемента)
    /// </summary>
    public readonly List<NiIconsElements> Elements = [];
}

public class NiIconsElements
{
    public string Name = "New Element";
    public bool IsEnabled = true;
    public int ContextMenuType = 1;
    public string Color = "FF6ACF";

    /// <summary>
    ///     Форма иконки:
    ///     0 - Куб, 1 - скруглённый куб, 2 - круг
    /// </summary>
    public int IconShape = 0;

    /// <summary>
    ///     Толщина шрифта:
    ///     0 - Обычный, 1 - Жирный
    /// </summary>
    public int FontWeight = 0;

    public int FontSize = 12;
    public double BgOpacity = 1.0d;
    public bool IsGradient = false;
    public string SecondColor = "143BB6";
    public string Guid = "";
}