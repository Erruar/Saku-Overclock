using System.Collections;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Services;

public partial class TrayCommandCollection : ITrayCommandCollection
{
    // Internal commands collection
    private readonly Dictionary<string, Action> _commands = [];

    /// <summary>
    /// Add new command to collection
    /// </summary>
    /// <param name="commandName">Name</param>
    /// <param name="action">Action</param>
    public void Add(string commandName, Action action)
    {
        _commands.Add(commandName, action);
    }


    public IEnumerator<KeyValuePair<string, Action>> GetEnumerator()
    {
        return _commands.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}