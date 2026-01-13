using System.Diagnostics;
using System.ServiceModel;
using Saku_Overclock.Models;

namespace Saku_Overclock.Wrappers;

public static class NotebookFanControlWrapper
{
    private static FanControlServiceClient? _client;
    private static readonly Lock Lock = new();
    private static bool _clientOpened;

    /// <summary>
    /// Открывает соединение с сервисом асинхронно
    /// </summary>
    public static async Task<bool> OpenClientAsync()
    {
        return await Task.Run(() =>
        {
            lock (Lock)
            {
                try
                {
                    // Если клиент существует, но в плохом состоянии - пересоздаём
                    if (_client != null && _client.State != CommunicationState.Opened)
                    {
                        DisposeFaultedClient();
                    }

                    // Если клиент уже открыт - всё хорошо
                    if (_client?.State == CommunicationState.Opened)
                    {
                        return true;
                    }

                    var binding = new NetNamedPipeBinding
                    {
                        Security = { Mode = NetNamedPipeSecurityMode.Transport },
                        TransferMode = TransferMode.Buffered,
                        OpenTimeout = TimeSpan.FromSeconds(5),
                        CloseTimeout = TimeSpan.FromSeconds(5),
                        SendTimeout = TimeSpan.FromSeconds(10),
                        ReceiveTimeout = TimeSpan.FromMinutes(10),
                        MaxReceivedMessageSize = 2147483647,
                        MaxBufferSize = 2147483647,
                        ReaderQuotas = 
                        {
                            MaxDepth = 32,
                            MaxStringContentLength = 2147483647,
                            MaxArrayLength = 2147483647,
                            MaxBytesPerRead = 2147483647,
                            MaxNameTableCharCount = 2147483647
                        }
                    };

                    var address = new EndpointAddress(
                        "net.pipe://localhost/StagWare.FanControl.Service/FanControlService");

                    _client = new FanControlServiceClient(binding, address);
                    _client.Open();

                    _clientOpened = true;
                    
                    return true;
                }
                catch (EndpointNotFoundException)
                {
                    Debug.WriteLine("NBFC Service не запущен или недоступен");
                    DisposeFaultedClient();
                    return false;
                }
                catch (TimeoutException)
                {
                    Debug.WriteLine("Таймаут при подключении к NBFC Service");
                    DisposeFaultedClient();
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при открытии клиента: {ex.Message}");
                    DisposeFaultedClient();
                    return false;
                }
            }
        });
    }

    public static void CloseClient()
    {
        lock (Lock)
        {
            if (_client == null)
            {
                return;
            }

            try
            {
                if (_client.State == CommunicationState.Opened)
                {
                    _client.Close();
                }
                else
                {
                    _client.Abort();
                }
            }
            catch
            {
                _client.Abort();
            }
            finally
            {
                _client = null;
                _clientOpened = false;
            }
        }
    }

    public static async Task ApplyConfigAsync(string configName)
    {
        await ExecuteServiceCallAsync(() =>
        {
            _client?.Stop();
            _client?.SetConfig(configName);
            _client?.Start(false);
        });
    }

    public static async Task<string[]> GetRecommendedConfigNamesAsync()
    {
        var result = Array.Empty<string>();
        await ExecuteServiceCallAsync(() =>
        {
            result = _client?.GetRecommendedConfigs() ?? [];
        });
        return result;
    }

    public static async Task StartServiceAsync(bool readOnly = false)
    {
        await ExecuteServiceCallAsync(() => _client?.Start(readOnly));
    }

    public static async Task StopServiceAsync()
    {
        await ExecuteServiceCallAsync(() => _client?.Stop());
    }

    public static async Task<bool> SetFanSpeedAsync(double speed, int idx)
    {
        if (!_clientOpened)
        {
            await OpenClientAsync();
        }
        return await ExecuteServiceCallAsync(() => 
            _client?.SetTargetFanSpeed((float)speed, idx));
    }

    public static async Task<double> GetFanSpeedAsync(int idx)
    {
        var info = await GetFanControlInfoAsync();
        return info?.FanStatus?[idx].CurrentFanSpeed ?? -1;
    }

    private static async Task<FanControlInfo?> GetFanControlInfoAsync()
    {
        FanControlInfo? result = null;
        await ExecuteServiceCallAsync(() =>
        {
            result = _client?.GetFanControlInfo();
        });
        return result;
    }

    private static async Task<bool> ExecuteServiceCallAsync(Action action)
    {
        return await Task.Run(() =>
        {
            lock (Lock)
            {
                try
                {
                    // Проверяем состояние перед каждым вызовом
                    if (_client?.State != CommunicationState.Opened)
                    {
                        return false;
                    }

                    action();
                    return true;
                }
                catch (CommunicationException ex)
                {
                    Debug.WriteLine($"Ошибка связи: {ex.Message}");
                    DisposeFaultedClient();
                    return false;
                }
                catch (TimeoutException ex)
                {
                    Debug.WriteLine($"Таймаут операции: {ex.Message}");
                    DisposeFaultedClient();
                    return false;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Ошибка при вызове сервиса: {ex.Message}");
                    DisposeFaultedClient();
                    return false;
                }
            }
        });
    }

    private static void DisposeFaultedClient()
    {
        if (_client == null)
        {
            return;
        }

        try
        {
            _client.Abort();
        }
        catch
        {
            // Игнорируем ошибки при Abort
        }
        finally
        {
            _client = null;
        }
    }
}