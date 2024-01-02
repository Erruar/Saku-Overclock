namespace Saku_Overclock.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
    bool Apply();
    void ApplyT();
}
