namespace Saku_Overclock.JsonContainers;

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

    public int FontSize = 9;
    public double BgOpacity = 1.0d;
    public bool IsGradient = false;
    public string SecondColor = "143BB6";
    public string Guid = "";
}