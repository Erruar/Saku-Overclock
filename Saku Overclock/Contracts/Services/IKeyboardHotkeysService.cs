using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IKeyboardHotkeysService : IDisposable
{
    /// <summary>
    ///     Initialize hotkeys service
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Enable hotkeys service
    /// </summary>
    void Enable();

    /// <summary>
    ///     Disable hotkeys service
    /// </summary>
    void Disable();

    /// <summary>
    ///     Preset changed event
    /// </summary>
    event EventHandler<PresetId> PresetChanged;
}