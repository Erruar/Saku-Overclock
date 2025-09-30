using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.ViewModels;

namespace Saku_Overclock.Activation;

public class DefaultActivationHandler(INavigationService navigationService)
    : ActivationHandler<LaunchActivatedEventArgs>
{
    protected override bool CanHandleInternal()
    {
        // None of the ActivationHandlers has handled the activation.
        return navigationService.Frame?.Content == null;
    }

    protected async override Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, args.Arguments);

        await Task.CompletedTask;
    }
}