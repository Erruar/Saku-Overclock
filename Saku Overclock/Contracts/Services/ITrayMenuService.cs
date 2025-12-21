namespace Saku_Overclock.Contracts.Services;
public interface ITrayMenuService : IDisposable
{
    void Initialize();
    void RegisterCommands(ITrayCommandCollection commands);
    void EnsureTrayIconCreated();
    void RestoreDefaultMenu();
    void SetMinimalMode();
}