namespace Saku_Overclock.Activation;

// Extend this class to implement new ActivationHandlers. See DefaultActivationHandler for an example.
// https://github.com/microsoft/TemplateStudio/blob/main/docs/WinUI/activation.md
public abstract class ActivationHandler<T> : IActivationHandler
    where T : class
{
    // Override this method to add the logic for whether to handle the activation.
    protected virtual bool CanHandleInternal() => true;

    // Override this method to add the logic for your activation handler.
    protected abstract Task HandleInternalAsync(T args);

    public bool CanHandle(object args) => args is T && CanHandleInternal();

    public async Task HandleAsync(object args) => await HandleInternalAsync((args as T)!);
}