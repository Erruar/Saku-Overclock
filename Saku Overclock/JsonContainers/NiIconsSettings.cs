namespace Saku_Overclock.JsonContainers;
internal class NiIconsSettings
{
    public List<NiIconsElements> Elements = []; // Полностью пустая коллекция (нет ни одного элемента)
}
public class NiIconsElements
{
    public string Name = "New Element";
    public bool IsEnabled = true;
    public int ContextMenuType = 1;
    public string Color = "FF6ACF";
    public int IconShape = 0; // 0 - Куб, 1 - скруглённый куб, 2 - круг, 3 - лист, 4 - звезда, 5 - Saku Overclock
    public int FontSize = 9;
    public double BgOpacity = 1.0d;
    public bool IsGradient = false;
    public string SecondColor = "143BB6";
    public string Guid = "";
}
