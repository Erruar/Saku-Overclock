namespace Saku_Overclock.Contracts.Services;
public interface ITrayCommandCollection : IEnumerable<KeyValuePair<string, Action>>
{
    void Add(string commandName, Action action);
}