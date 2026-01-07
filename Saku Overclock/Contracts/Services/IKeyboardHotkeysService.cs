using static Saku_Overclock.Services.PresetManagerService;

namespace Saku_Overclock.Contracts.Services;

public interface IKeyboardHotkeysService : IDisposable
{
    /// <summary>
    ///     Инициализировать сервис горячих клавиш
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Включить сервис горячих клавиш
    /// </summary>
    void Enable();

    /// <summary>
    ///     Выключить сервис горячих клавиш
    /// </summary>
    void Disable();

    /// <summary>
    ///     Событие смены пресета
    /// </summary>
    event EventHandler<PresetId> PresetChanged;
}