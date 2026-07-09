namespace Saku_Overclock.Activation;

public interface IActivationHandler
{
    /// <summary>
    ///     Handling supported
    /// </summary>
    /// <param name="args">Handling args</param>
    /// <returns>Boolean</returns>
    bool CanHandle(object args);

    /// <summary>
    ///     Handle event
    /// </summary>
    /// <param name="args">Handling args</param>
    Task HandleAsync(object args);
}
