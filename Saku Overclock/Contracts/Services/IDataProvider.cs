using System.Threading.Tasks;
using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.Contracts.Services;

public interface IDataProvider
{
    /// <summary>
    /// Асинхронно получает обновлённые данные.
    /// </summary>
    Task<SensorsInformation> GetDataAsync();
}
