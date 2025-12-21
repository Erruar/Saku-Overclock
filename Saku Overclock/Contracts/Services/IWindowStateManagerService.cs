namespace Saku_Overclock.Contracts.Services;
public interface IWindowStateManagerService
{
    void Initialize();
    void ToggleWindowVisibility();
    void ShowMainWindow();
}