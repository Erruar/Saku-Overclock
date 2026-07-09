using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using System.Text;
using Saku_Overclock.ViewModels;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Helpers;

internal static class LogHelper
{
    private static readonly SemaphoreSlim LogSemaphore = new(1, 1);
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();

    /// <summary>
    ///     Show Error message in dialog
    /// </summary>
    /// <param name="ex">Exception</param>
    /// <param name="xamlRoot">Page root</param>
    public static async Task ShowErrorMessageAndLog(Exception ex, XamlRoot xamlRoot)
    {
        var errorMessage = $"{ex.Message}\nStack Trace: {ex.StackTrace}";

        await LogError(errorMessage); // Log error
        await ShowErrorDialog(errorMessage, xamlRoot); // Show error dialog
    }
    
    /// <summary>
    ///     Log error and show in UI
    /// </summary>
    /// <param name="error">Error sting</param>
    /// <returns>Task result</returns>
    public static Task TraceIt_TraceError(string error)
    {
        _ = Task.Run(async () => 
        {
            await LogError(error);
            if (error != string.Empty)
            {
                NotificationsService.ShowNotification("TraceIt_Error".GetLocalized(),
                    error,
                    InfoBarSeverity.Error);
            }
        }); 
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Log error and show in UI
    /// </summary>
    /// <param name="exception">Exception</param>
    /// <returns>Task result</returns>
    public static Task TraceIt_TraceError(Exception exception)
    {
        var error = exception.ToString();
        return TraceIt_TraceError(error);
    }

    /// <summary>
    ///     Log info
    /// </summary>
    /// <param name="message">Information message</param>
    /// <returns>Task result</returns>
    public static Task Log(string message) => LogToFile($"[DEBUG] {message}", "Logs");

    /// <summary>
    ///     Log warning
    /// </summary>
    /// <param name="message">Warning message</param>
    /// <returns>Task result</returns>
    public static Task LogWarn(string message) => LogToFile($"[WARNING] {message}", "Logs");
    
    /// <summary>
    ///     Log warning
    /// </summary>
    /// <param name="exception">Exception</param>
    /// <returns>Task result</returns>
    public static Task LogWarn(Exception exception) => 
        LogToFile(
            "[WARNING] exception: " + exception +
            (
                exception.InnerException != null &&
                !string.IsNullOrWhiteSpace(exception.InnerException.Message)
                    ? "\ninner exception: " + exception.InnerException.Message
                    : string.Empty
            ),
            "Logs"
        );

    /// <summary>
    ///     Log error
    /// </summary>
    /// <param name="message">Error message</param>
    /// <returns>Task result</returns>
    public static Task LogError(string message) => LogToFile($"[ERROR] {message}", "Logs");
    
    /// <summary>
    ///     Log error
    /// </summary>
    /// <param name="exception">Exception</param>
    /// <returns>Task result</returns>
    public static Task LogError(Exception exception) =>
        LogToFile(
            "[ERROR] exception: " + exception +
            (
                exception.InnerException != null &&
                !string.IsNullOrWhiteSpace(exception.InnerException.Message)
                    ? "\ninner exception: " + exception.InnerException.Message
                    : string.Empty
            ),
            "Logs"
        );

    private static async Task ShowErrorDialog(string errorMessage, XamlRoot xamlRoot)
    {
        await LogSemaphore.WaitAsync();
        try
        {
            var errorDialog = new ContentDialog
            {
                Title = "Error",
                Style = (Style)Application.Current.Resources["DefaultContentDialogStyle"],
                Content = errorMessage,
                CloseButtonText = "Close",
                PrimaryButtonText = "Open Logs File",
                XamlRoot = xamlRoot
            };

            errorDialog.PrimaryButtonClick += async (_, _) =>
            {
                var logFile = await GetLogFile($"ErrorLogs_{DateTime.Now:yyyy-MM-dd}.txt");
                await Windows.System.Launcher.LaunchFileAsync(logFile);
            };

            await errorDialog.ShowAsync();
        }
        finally
        {
            LogSemaphore.Release();
        }
    }

    private static async Task<StorageFile?> GetLogFile(string fileName)
    {
        try
        {
            var personalFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var logFolderPath = Path.Combine(personalFolder, "SakuOverclock");

            // If log folder not exist - create it
            var logFolder = await StorageFolder.GetFolderFromPathAsync(logFolderPath).AsTask()
                .ContinueWith(async t =>
                {
                    try
                    {
                        return await t;
                    }
                    catch
                    {
                        return await StorageFolder.GetFolderFromPathAsync(personalFolder).AsTask()
                            .ContinueWith(async parentFolderTask =>
                            {
                                var parentFolder = await parentFolderTask;
                                return await parentFolder.CreateFolderAsync("SakuOverclock",
                                    CreationCollisionOption.OpenIfExists);
                            }).Unwrap();
                    }
                }).Unwrap();

            // Create log file
            return await logFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
        }
        catch
        {
            return null;
        }
    }

    private static async Task LogToFile(string message, string fileName)
    {
        await LogSemaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            Debug.WriteLine(message);

            var logFile = await GetLogFile($"{fileName}_{ГлавнаяViewModel.GetPublicVersionDescription()}_{ГлавнаяViewModel.GetVersion()}.txt")
                .ConfigureAwait(false);

            if (logFile == null)
            {
                return;
            }

            try
            {
                using var stream = await logFile.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false);
                using var outputStream = stream.GetOutputStreamAt(stream.Size);
                await using var writer = new StreamWriter(outputStream.AsStreamForWrite(), new UTF8Encoding(false));

                await writer.WriteLineAsync($"{DateTime.Now:T}: {message}").ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                await outputStream.FlushAsync().AsTask().ConfigureAwait(false);
                await stream.FlushAsync().AsTask().ConfigureAwait(false);
            }
            catch (Exception ex) when (
                ex is ObjectDisposedException 
                    or UnauthorizedAccessException 
                    or IOException or TaskCanceledException)
            {
                Debug.WriteLine($"[LOG WRITE FAILED] {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LOG FAILED] {ex.Message}");
        }
        finally
        {
            LogSemaphore.Release();
        }
    }
}