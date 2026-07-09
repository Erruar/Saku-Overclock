using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32.TaskScheduler;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Helpers;

internal abstract class AutoStartHelper
{
    private const string TaskName = "Saku Overclock";
    private const string TaskDescription = "An awesome ryzen laptop overclock utility for those who want real performance! Autostart Saku Overclock application task";
    private const string TaskAuthor = "Sakura Serzhik";

    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();

    /// <summary>
    ///     Check autostart task and fix it if needed
    /// </summary>
    public static void AutoStartCheckAndFix()
    {
        using var taskService = new TaskService();
        var executablePath = GetExecutablePath();

        if (AppSettings.AutostartType is 1 or 2)
        {
            var existingTask = taskService.GetTask(TaskName);

            if (IsTaskValid(existingTask, executablePath))
            {
                return; // Task valid
            }

            RemoveTaskIfExists(taskService, TaskName);
            CreateStartupTask(taskService, executablePath);
        }
        else
        {
            // If autostart disabled - just remove existing task
            RemoveTaskIfExists(taskService, TaskName);
        }
    }

    /// <summary>
    ///     Create autostart task in task service
    /// </summary>
    public static void SetStartupTask()
    {
        using var taskService = new TaskService();
        var executablePath = GetExecutablePath();

        RemoveTaskIfExists(taskService, TaskName);
        CreateStartupTask(taskService, executablePath);
    }

    /// <summary>
    ///     Remove autostart task from task service
    /// </summary>
    public static void RemoveStartupTask()
    {
        using var taskService = new TaskService();
        RemoveTaskIfExists(taskService, TaskName);
    }

    /// <summary>
    ///     Get installed app path
    /// </summary>
    /// <returns>Path to Saku Overclock.exe</returns>
    private static string GetExecutablePath()
    {
        return Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "SakuOverclock.exe");
    }

    /// <summary>
    ///     Validate task in task service
    /// </summary>
    /// <returns>Validation result</returns>
    private static bool IsTaskValid(Microsoft.Win32.TaskScheduler.Task? task, string expectedPath)
    {
        if (task == null)
        {
            return false;
        }

        // Check file path
        if (task.Definition.Actions.Count == 0 ||
            task.Definition.Actions[0] is not ExecAction execAction)
        {
            return false;
        }

        return execAction.Path.Equals(expectedPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Create autostart task in task service
    /// </summary>
    private static void CreateStartupTask(TaskService taskService, string executablePath)
    {
        var taskDefinition = taskService.NewTask();

        // Main task info
        taskDefinition.RegistrationInfo.Description = TaskDescription;
        taskDefinition.RegistrationInfo.Author = TaskAuthor;
        taskDefinition.RegistrationInfo.Version = new Version("1.0.0");

        // Run rights
        taskDefinition.Principal.RunLevel = TaskRunLevel.Highest;

        // Trigger: when any user logon system
        taskDefinition.Triggers.Add(new LogonTrigger { Enabled = true });

        // Action: start executable
        taskDefinition.Actions.Add(new ExecAction(executablePath));

        // Advanced settings
        taskDefinition.Settings.DisallowStartIfOnBatteries = false; // Allow start from battery (fix for old bug)
        taskDefinition.Settings.StopIfGoingOnBatteries = false;
        taskDefinition.Settings.AllowDemandStart = true;
        taskDefinition.Settings.StartWhenAvailable = true;
        taskDefinition.Settings.AllowHardTerminate = false;
        taskDefinition.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
        taskDefinition.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        taskDefinition.Settings.Priority = ProcessPriorityClass.Normal;

        // Register task
        taskService.RootFolder.RegisterTaskDefinition(
            TaskName,
            taskDefinition,
            TaskCreation.CreateOrUpdate,
            null, // Use current user
            null,
            TaskLogonType.InteractiveToken
        );
    }

    /// <summary>
    ///     Remove autostart task from task service
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
        catch (Exception ex)
        {
            LogHelper.LogError(ex);
        }
    }
}