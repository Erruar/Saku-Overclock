namespace Saku_Overclock.Models;

/// <summary>
/// Данные P-State независимо от архитектуры CPU
/// </summary>
public class PstateData
{
    public int StateNumber
    {
        get; set;
    } // 0, 1, 2
    public bool IsEnabled
    {
        get; set;
    }
    public double FrequencyMHz
    {
        get; set;
    }
    public double VoltageMillivolts
    {
        get; set;
    }
    public uint Fid
    {
        get; set;
    }
    public uint Did
    {
        get; set;
    }
    public uint Vid
    {
        get; set;
    }
    public uint IddValue
    {
        get; set;
    }
    public uint IddDiv
    {
        get; set;
    }

    /// <summary>
    /// Множитель CPU (для UI)
    /// </summary>
    public double Multiplier => FrequencyMHz / 100.0;
}

/// <summary>
/// Результат операции с P-State
/// </summary>
public class PstateOperationResult
{
    public bool Success
    {
        get; set;
    }
    public string? ErrorMessage
    {
        get; set;
    } = string.Empty;
    public PstateData Data
    {
        get; set;
    } = new PstateData();

    public static PstateOperationResult Ok(PstateData data) =>
        new()
        {
            Success = true,
            Data = data
        };

    public static PstateOperationResult Fail(string? error) =>
        new()
        {
            Success = false,
            ErrorMessage = error
        };
}

/// <summary>
/// Параметры для записи P-State
/// </summary>
public class PstateWriteParams
{
    public int StateNumber
    {
        get; set;
    }
    public double FrequencyMHz
    {
        get; set;
    }
    public double VoltageMillivolts
    {
        get; set;
    }
    public bool? Enable
    {
        get; set;
    }
}
