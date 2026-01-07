using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32.TaskScheduler;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Helpers;

/// <summary>
/// Вспомогательный класс для управления автозапуском приложения Saku Overclock через планировщик заданий Windows
/// </summary>
internal class AutoStartHelper
{
    private const string TaskName = "Saku Overclock";
    private const string TaskDescription = "An awesome ryzen laptop overclock utility for those who want real performance! Autostart Saku Overclock application task";
    private const string TaskAuthor = "Sakura Serzhik";

    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();

    /// <summary>
    /// Проверяет наличие задачи автозапуска и исправляет её при необходимости.
    /// Создаёт новую задачу, если требуется автозапуск, или удаляет существующую, если автозапуск отключен.
    /// </summary>
    public static void AutoStartCheckAndFix()
    {
        using var taskService = new TaskService();
        var executablePath = GetExecutablePath();

        if (AppSettings.AutostartType == 2 || AppSettings.AutostartType == 3)
        {
            var existingTask = taskService.GetTask(TaskName);

            if (IsTaskValid(existingTask, executablePath))
            {
                return; // Задача корректна, ничего не делаем
            }

            // Удаляем старую задачу, если она существует
            RemoveTaskIfExists(taskService, TaskName);

            // Создаём новую задачу с правильными настройками
            CreateStartupTask(taskService, executablePath);
        }
        else
        {
            // Автозапуск отключен - удаляем задачу, если она существует
            RemoveTaskIfExists(taskService, TaskName);
        }
    }

    /// <summary>
    /// Создаёт задачу автозапуска приложения в планировщике заданий Windows.
    /// Задача запускается при входе пользователя в систему с максимальными правами.
    /// </summary>
    public static void SetStartupTask()
    {
        using var taskService = new TaskService();
        var executablePath = GetExecutablePath();

        // Удаляем старую задачу, если она существует
        RemoveTaskIfExists(taskService, TaskName);

        // Создаём новую задачу
        CreateStartupTask(taskService, executablePath);
    }

    /// <summary>
    /// Удаляет задачу автозапуска из планировщика заданий Windows, если она существует
    /// </summary>
    public static void RemoveStartupTask()
    {
        using var taskService = new TaskService();
        RemoveTaskIfExists(taskService, TaskName);
    }

    /// <summary>
    /// Получает полный путь к исполняемому файлу приложения
    /// </summary>
    /// <returns>Полный путь к Saku Overclock.exe</returns>
    private static string GetExecutablePath()
    {
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        var programDirectory = Path.GetDirectoryName(assemblyLocation);
        return Path.Combine(programDirectory!, "Saku Overclock.exe");
    }

    /// <summary>
    /// Проверяет, является ли существующая задача валидной и актуальной
    /// </summary>
    /// <returns>True, если задача валидна; иначе False</returns>
    private static bool IsTaskValid(Microsoft.Win32.TaskScheduler.Task? task, string expectedPath)
    {
        if (task == null)
        {
            return false;
        }

        // Проверяем путь к исполняемому файлу
        if (task.Definition.Actions.Count == 0 ||
            task.Definition.Actions[0] is not ExecAction execAction)
        {
            return false;
        }

        return execAction.Path.Equals(expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Создаёт новую задачу автозапуска в планировщике заданий с оптимальными настройками
    /// </summary>
    private static void CreateStartupTask(TaskService taskService, string executablePath)
    {
        var taskDefinition = taskService.NewTask();

        // Основная информация о задаче
        taskDefinition.RegistrationInfo.Description = TaskDescription;
        taskDefinition.RegistrationInfo.Author = TaskAuthor;
        taskDefinition.RegistrationInfo.Version = new Version("1.0.0");

        // Запуск с правами администратора
        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;

        // Триггер: запуск при входе пользователя в систему
        taskDefinition.Triggers.Add(new LogonTrigger { Enabled = true });

        // Действие: запуск исполняемого файла
        taskDefinition.Actions.Add(new ExecAction(executablePath));

        // Дополнительные настройки
        taskDefinition.Settings.DisallowStartIfOnBatteries = false;  // Разрешить запуск от батареи
        taskDefinition.Settings.StopIfGoingOnBatteries = false;      // НЕ останавливать при переходе на батарею
        taskDefinition.Settings.AllowDemandStart = true;             // Разрешить ручной запуск
        taskDefinition.Settings.StartWhenAvailable = true;           // Запустить при первой возможности
        taskDefinition.Settings.AllowHardTerminate = false;          // Не убивать задачу принудительно
        taskDefinition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew; // Не запускать дубликаты
        taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;  // Без ограничения времени выполнения
        taskDefinition.Settings.Priority = ProcessPriorityClass.Normal; // Нормальный приоритет

        // Регистрируем задачу
        taskService.RootFolder.RegisterTaskDefinition(
            TaskName,
            taskDefinition,
            TaskCreation.CreateOrUpdate, // Создать или обновить
            null, // Использовать текущего пользователя
            null,
            TaskLogonType.InteractiveToken // Интерактивный токен для GUI приложений
        );
    }

    /// <summary>
    /// Безопасно удаляет задачу из планировщика, если она существует
    /// </summary>
    private static void RemoveTaskIfExists(TaskService taskService, string taskName)
    {
        try
        {
            var existingTask = taskService.GetTask(taskName);
            if (existingTask != null)
            {
                taskService.RootFolder.DeleteTask(taskName, false);
            }
        }
        catch
        {
            // Игнорируем ошибки при удалении (задача может не существовать или нет прав доступа)
        }
    }
}