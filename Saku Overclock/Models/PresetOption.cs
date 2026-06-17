using System.Text.Json.Serialization;

namespace Saku_Overclock.Models;

public class PresetOption<T>
{
    public bool IsEnabled { get; set; }
    public T Value { get; set; } = default!;

    // 1. Пустой конструктор для System.Text.Json — теперь без ошибок компиляции!
    [JsonConstructor]
    public PresetOption() { }

    // 2. Классический аналог твоего бывшего первичного конструктора
    public PresetOption(bool isEnabled, T value)
    {
        IsEnabled = isEnabled;
        Value = value;
    }

    // 3. Твой дополнительный конструктор (перенаправляет вызов на конструктор выше)
    public PresetOption(T value) : this(false, value) { }
}