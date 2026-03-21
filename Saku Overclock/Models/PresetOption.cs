namespace Saku_Overclock.Models;

public class PresetOption<T>(bool isEnabled, T value)
{
    public bool IsEnabled { get; set; } = isEnabled;
    public T Value { get; set; } = value;

    public PresetOption(T value) : this(false, value) { }
}