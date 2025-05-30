using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using System.Text;
using Saku_Overclock.ViewModels;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Contracts.Services;

namespace Saku_Overclock.Helpers;

internal static class LogHelper
{
    private static readonly SemaphoreSlim LogSemaphore = new(1, 1);
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>();

    public static async Task ShowErrorMessageAndLog(Exception ex, XamlRoot xamlRoot)
    {
        var errorMessage = $"{ex.Message}\nStack Trace: {ex.StackTrace}";

        await LogError(errorMessage); // Логируем ошибку
        await ShowErrorDialog(errorMessage, xamlRoot); // Показываем диалог с ошибкой
    }

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
            // Получаем путь к папке для логов
            var personalFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            var logFolderPath = Path.Combine(personalFolder, "SakuOverclock");

            // Создаём папку, если её нет
            var logFolder = await StorageFolder.GetFolderFromPathAsync(logFolderPath).AsTask()
                .ContinueWith(async t =>
                {
                    try
                    {
                        return await t;
                    }
                    catch
                    {
                        // Если папка не существует, создаём её
                        return await StorageFolder.GetFolderFromPathAsync(personalFolder).AsTask()
                            .ContinueWith(async parentFolderTask =>
                            {
                                var parentFolder = await parentFolderTask;
                                return await parentFolder.CreateFolderAsync("SakuOverclock",
                                    CreationCollisionOption.OpenIfExists);
                            }).Unwrap();
                    }
                }).Unwrap();

            // Создаём файл лога
            return await logFolder.CreateFileAsync(fileName, CreationCollisionOption.OpenIfExists);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error getting log file: {ex.Message}");
            return null;
        }
    }

    private static async Task LogToFile(string message, string fileName)
    {
        await LogSemaphore.WaitAsync();
        try
        {
            Debug.WriteLine($"Attempting to log: {message}");

            var logFile = await GetLogFile($"{fileName}_{ГлавнаяViewModel.GetPublicVersionDescription()}_{ГлавнаяViewModel.GetVersion()}.txt");
            if (logFile == null)
            {
                Debug.WriteLine("Log file could not be created.");
                return;
            }

            Debug.WriteLine($"Log file path: {logFile.Path}");

            // Открываем файл в режиме добавления (Append)
            using (var stream = await logFile.OpenAsync(FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false))
            {
                using (var outputStream = stream.GetOutputStreamAt(stream.Size))
                {
                    using (var writer = new StreamWriter(outputStream.AsStreamForWrite(), new UTF8Encoding(false)))
                    {
                        await writer.WriteLineAsync($"{DateTime.Now:T}: {message}");
                        await writer.FlushAsync(); // Принудительно сбрасываем буфер
                    }

                    // Явно закрываем outputStream
                    await outputStream.FlushAsync();
                }

                // Явно закрываем stream
                await stream.FlushAsync();
            }

            Debug.WriteLine("Message written to log file.");
        }
        catch (Exception logException)
        {
            Debug.WriteLine($"Error logging to file: {logException.Message}");
        }
        finally
        {
            LogSemaphore.Release();
        }
    }

    public static Task TraceIt_TraceError(string error) //Система TraceIt! позволит логгировать все ошибки
    {
        _ = Task.Run(async () => 
        {
            await LogError(error);
            if (error != string.Empty)
            {
                NotificationsService.Notifies ??= [];
                NotificationsService.Notifies.Add(new Notify
                {
                    Title = "TraceIt_Error".GetLocalized(),
                    Msg = error,
                    Type = InfoBarSeverity.Error
                });
                NotificationsService.SaveNotificationsSettings();
            }
        }); 
        return Task.CompletedTask;
    }

    public static Task Log(string message) => LogToFile($"[DEBUG] {message}", "Logs");
    public static Task LogWarn(string message) => LogToFile($"[WARNING] {message}", "Logs");
    public static Task LogError(string message) => LogToFile($"[ERROR] {message}", "Logs");
}