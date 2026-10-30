using Microsoft.UI.Xaml;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
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

    protected override async Task HandleInternalAsync(LaunchActivatedEventArgs args)
    {
        // Redirect the user to the app's initial setup page upon the first launch
        if (FirstRunManager.GetFirstRun()) navigationService.NavigateTo(typeof(ОбучениеViewModel).FullName!);
        else navigationService.NavigateTo(typeof(ГлавнаяViewModel).FullName!, args.Arguments);

        await Task.CompletedTask;
    }
}