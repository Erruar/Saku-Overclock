namespace Saku_Overclock.Models;

public class PresetDisplayItem
{
    /// <summary>
    ///     Preset guid
    /// </summary>
    public string Id { get; init; } = string.Empty;
    
    /// <summary>
    ///     Preset name
    /// </summary>
    public string Name { get; init; } = string.Empty;

    // ComboBox automatically use this method to display string
    public override string ToString()
    {
        return Name;
    }
}