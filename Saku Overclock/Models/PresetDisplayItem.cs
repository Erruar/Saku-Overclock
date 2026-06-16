namespace Saku_Overclock.Models;

public class PresetDisplayItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    // ComboBox автоматически вызывает этот метод, чтобы получить текст для отображения
    public override string ToString()
    {
        return Name;
    }
}