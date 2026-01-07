using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Models;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.Services;

public class PstateService(
    ICpuService cpuService,
    IEnumerable<IPstateStrategy> strategies) : IPstateService
{
    private IPstateStrategy? _currentStrategy;

    public CpuFamily CurrentFamily
    {
        get;
        private set;
    }

    public bool IsSupported => _currentStrategy != null;

    public void Initialize()
    {
        try
        {
            CurrentFamily = cpuService.Family;

            if (CurrentFamily < CpuFamily.Family17H)
            {
                LogHelper.LogError($"P-States not supported for CPU family {CurrentFamily}");
            }

            // Определяем стратегию на основе семейства CPU
            _currentStrategy = strategies.FirstOrDefault(s => s.IsSupportedFamily);

            if (_currentStrategy == null)
            {
                LogHelper.LogError($"No P-State strategy found for CPU family {CurrentFamily}");
            }
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    public IReadOnlyList<PstateOperationResult> ReadAllPstates()
    {
        if (!EnsureInitialized() || _currentStrategy == null)
        {
            return [];
        }

        var results = new List<PstateOperationResult>();
        for (var i = 0; i < 3; i++)
        {
            results.Add(_currentStrategy.ReadPstate(i));
        }

        return results;
    }

    public PstateOperationResult ReadPstate(int stateNumber)
    {
        if (!EnsureInitialized() || _currentStrategy == null)
        {
            return PstateOperationResult.Fail("Service not initialized");
        }

        return _currentStrategy.ReadPstate(stateNumber);
    }

    public PstateOperationResult WritePstate(PstateWriteParams parameters)
    {
        if (!EnsureInitialized() || _currentStrategy == null)
        {
            return PstateOperationResult.Fail("Service not initialized");
        }

        return _currentStrategy.WritePstate(parameters);
    }

    public bool WritePstates(IEnumerable<PstateWriteParams> pstates)
    {
        if (!EnsureInitialized() || _currentStrategy == null)
        {
            return false;
        }

        try
        {
            var results = new List<bool>();
            foreach (var pstate in pstates)
            {
                var result = _currentStrategy.WritePstate(pstate);
                results.Add(result.Success);

                if (!result.Success)
                {
                    LogHelper.LogError($"Failed to write P-State {pstate.StateNumber}: {result.ErrorMessage}");
                }
            }

            return results.All(r => r);
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex);
            return false;
        }
    }

    private bool EnsureInitialized()
    {
        if (_currentStrategy != null)
        {
            return true;
        }

        LogHelper.LogError("P-State service not initialized. Call Initialize() first.");
        return false;
    }
}