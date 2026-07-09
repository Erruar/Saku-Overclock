namespace Saku_Overclock.Activation;

public abstract class ActivationHandler<T> : IActivationHandler
    where T : class
{
    /// <summary>
    ///     Internal handling supported
    /// </summary>
    /// <returns>Boolean</returns>
    protected virtual bool CanHandleInternal() => true;

    /// <summary>
    ///     Internal code for handling
    /// </summary>
    /// <param name="args">Task arguments</param>
    /// <returns>Task result</returns>
    protected abstract Task HandleInternalAsync(T args);

    /// <summary>
    ///     Handling supported
    /// </summary>
    /// <param name="args">Handling args</param>
    /// <returns>Boolean</returns>
    public bool CanHandle(object args) => args is T && CanHandleInternal();

    /// <summary>
    ///     Handle event
    /// </summary>
    /// <param name="args">Handling args</param>
    public async Task HandleAsync(object args) => await HandleInternalAsync((args as T)!);
}