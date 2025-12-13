using Saku_Overclock.SmuEngine;

namespace Saku_Overclock.Contracts.Services;
public interface ISensorIndexResolver
{
    int ResolveIndex(int tableVersion, SensorId sensor);
}
