using System.Collections;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Services;

public partial class TrayCommandCollection : ITrayCommandCollection
{
    // Внутренняя коллекция для хранения команд.
    private readonly Dictionary<string, Action> _commands = [];

    /// <summary>
    /// Добавляет новую команду в коллекцию.
    /// Этот метод необходим для работы синтаксиса инициализации коллекции: new TrayCommandCollection { { "Name", Action }, ... }
    /// </summary>
    /// <param name="commandName">Уникальное имя команды.</param>
    /// <param name="action">Действие (метод) для выполнения.</param>
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