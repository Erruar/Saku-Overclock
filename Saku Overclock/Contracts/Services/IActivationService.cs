namespace Saku_Overclock.Contracts.Services;

public interface IActivationService
{
    /// <summary>
    ///     Activate client services
    /// </summary>
    /// <param name="activationArgs">Activation args</param>
    /// <returns>Task result</returns>
    Task ActivateAsync(object activationArgs);
}