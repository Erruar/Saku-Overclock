namespace Saku_Overclock.Contracts.Services;
public interface IKeyboardHotkeysService : IDisposable
{
    void Initialize();
    void Enable();
    void Disable();
}