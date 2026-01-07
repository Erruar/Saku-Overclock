using Saku_Overclock.Models;

namespace Saku_Overclock.Contracts.Services;

public interface IPstateStrategy
{
    /// <summary>
    ///     Поддерживаемое семейство CPU
    /// </summary>
    bool IsSupportedFamily
    {
        get;
    }

    /// <summary>
    ///     Прочитать P-State из MSR
    /// </summary>
    PstateOperationResult ReadPstate(int stateNumber);

    /// <summary>
    ///     Записать P-State в MSR
    /// </summary>
    PstateOperationResult WritePstate(PstateWriteParams parameters);

    /// <summary>
    ///     Валидация параметров для данной архитектуры
    /// </summary>
    bool ValidateParameters(PstateWriteParams parameters, out string? error);
}