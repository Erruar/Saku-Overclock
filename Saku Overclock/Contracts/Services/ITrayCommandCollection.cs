namespace Saku_Overclock.Contracts.Services;
public interface ITrayCommandCollection : IEnumerable<KeyValuePair<string, Action>>
{
    /// <summary>
    ///     Add tray command to app tray menu
    /// </summary>
    /// <param name="commandName">Command name</param>
    /// <param name="action">Used action</param>
    void Add(string commandName, Action action);
}