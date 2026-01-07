using Saku_Overclock.Models;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.Contracts.Services;

public interface IPstateService
{
    /// <summary>
    ///     Инициализация сервиса и определение архитектуры процессора
    /// </summary>
    void Initialize();

    /// <summary>
    ///     Прочитать все P-States (0, 1, 2)
    /// </summary>
    IReadOnlyList<PstateOperationResult> ReadAllPstates();

    /// <summary>
    ///     Прочитать конкретный P-State
    /// </summary>
    PstateOperationResult ReadPstate(int stateNumber);

    /// <summary>
    ///     Записать P-State
    /// </summary>
    PstateOperationResult WritePstate(PstateWriteParams parameters);

    /// <summary>
    ///     Применить пресет (набор всех P-States)
    /// </summary>
    bool WritePstates(IEnumerable<PstateWriteParams> pstates);

    /// <summary>
    ///     Текущая архитектура CPU
    /// </summary>
    CpuFamily CurrentFamily
    {
        get;
    }

    /// <summary>
    ///     Проверка поддержки P-States
    /// </summary>
    bool IsSupported
    {
        get;
    }
}