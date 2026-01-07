namespace Saku_Overclock.Contracts.Services;

public interface IActivationService
{
    /// <summary>
    /// Активировать сервисы приложения
    /// </summary>
    /// <param name="activationArgs">Аргументы при запуске программы</param>
    /// <returns>Результат выполнения задачи</returns>
    Task ActivateAsync(object activationArgs);
}