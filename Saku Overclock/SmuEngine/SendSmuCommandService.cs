using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.Views;
using ZenStates.Core;
using static ZenStates.Core.Cpu;

namespace Saku_Overclock.SMUEngine;

/*Created by Serzhik Sakurazhima*/
/*Этот класс является частью программного обеспечения для управления разгоном процессоров.Он содержит методы и данные для отправки команд SMU (System Management Unit) на процессор.Вот краткое описание содержимого класса:

Приватные статические поля:

RsmuRsp, RsmuArg, RsmuCmd: содержат адреса для отправки команд SMU на системы AMD.
Mp1Rsp, Mp1Arg, Mp1Cmd: содержат адреса для отправки команд SMU на системы с другой архитектурой.
Публичные свойства и поля:

Commands: список кортежей, хранящих информацию о командах (название, флаг применения, адрес).
_cancelrange: флаг для отмены диапазона команд.
Методы:


SmuSettingsLoad, ProfileLoad: методы для сохранения и загрузки настроек из JSON-файлов.
Play_Invernate_QuickSMU, ApplySettings, ApplyThis, SendRange: устаревшие методы, отвечающие за отправку команд SMU на процессор.
JsonRepair: метод для восстановления JSON-файлов в случае их повреждения.
CancelRange: устанавливает флаг отмены диапазона команд.
Приватные методы для определения адресов и отправки команд SMU на процессор.
Этот класс является частью большой системы, управляющей разгоном процессоров, и содержит функционал для загрузки/сохранения настроек, определения возможности разгона на данной архитектуре и отправки соответствующих команд на процессор.*/
public class SendSmuCommandService : ISendSmuCommandService
{
    private static uint RsmuRsp
    {
        get;
        set;
    }

    private static uint RsmuArg
    {
        get;
        set;
    }

    private static uint RsmuCmd
    {
        get;
        set;
    }

    private static uint Mp1Rsp
    {
        get;
        set;
    }

    private static uint Mp1Arg
    {
        get;
        set;
    }

    private static uint Mp1Cmd
    {
        get;
        set;
    }

    private static List<(string, bool, uint)>? Commands
    {
        get;
        set;
    }

    private bool _saveinfo;
    private Cpu? _cpu;
    private static CodeName Codename;
    private static readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private static string _cpuCodenameString = string.Empty;
    private Smusettings _smusettings = new();
    private readonly Mailbox _testMailbox = new();
    private Profile[] _profile = new Profile[1];
    private bool _cancelrange;
    private bool _dangersettingsapplied;
    private string _checkAdjLine = string.Empty;
    private bool _isBatteryUnavailable = true;
    private bool? _isOlderGeneration = null;
    private bool _unsupportedPlatformMessage = false;

    private bool SafeReapply
    {
        get;
        set;
    } = true;

    private static string _codenameGeneration = "None";

    public SendSmuCommandService()
    {
        try
        {
            _cpu ??= CpuSingleton.GetInstance();
            Codename = _cpu.info.codeName;
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError("[SendSmuCommand]@Launch_Get_ZenStates_Core - " + ex.ToString());
        }

        var dataUpdater = App.BackgroundUpdater;
        if (dataUpdater != null)
        {
            dataUpdater.DataUpdated += OnDataUpdated;
        }
        else
        {
            _cpuCodenameString = _cpu != null ? _cpu.info.codeName.ToString() : "Unsupported";
        }

        try
        {
            SafeReapply = AppSettings.ReapplySafeOverclock;
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private void OnDataUpdated(object? sender, SensorsInformation info)
    {
        _cpuCodenameString =
        _cpuCodenameString == (info.CpuFamily ?? "Unsupported") ? _cpuCodenameString : info.CpuFamily ?? "Unsupported";
        _isBatteryUnavailable = info.BatteryUnavailable;
    }
    public void Init(Cpu? cpu = null)
    {
        if (cpu == null)
        {
            _cpu ??= CpuSingleton.GetInstance();
        }
        _cpu = cpu;
    }
    public void SetCpuCodename(CodeName codename)
    {
        Codename = codename;
    }
    public bool GetSetSafeReapply(bool? value = null)
    {
        if (value != null)
        {
            SafeReapply = value == true;
        }
        return SafeReapply;
    }

    // JSON
    private void SmuSettingsLoad()
    {
        try
        {
            _smusettings = JsonConvert.DeserializeObject<Smusettings>(File.ReadAllText(
                               Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                               @"\SakuOverclock\smusettings.json")) ??
                           new Smusettings();
        }
        catch (Exception ex)
        {
            JsonRepair('s');
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private void ProfileLoad()
    {
        try
        {
            _profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json")) ?? [];
        }
        catch (Exception ex)
        {
            JsonRepair('p');
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    private void JsonRepair(char file)
    {
        switch (file)
        {
            case 's':
                {
                    _smusettings = new Smusettings();
                    try
                    {
                        Directory.CreateDirectory(
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                        File.WriteAllText(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                            @"\SakuOverclock\smusettings.json",
                            JsonConvert.SerializeObject(_smusettings, Formatting.Indented));
                    }
                    catch
                    {
                        File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                    @"\SakuOverclock\smusettings.json");
                        Directory.CreateDirectory(
                            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                        File.WriteAllText(
                            Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                            @"\SakuOverclock\smusettings.json",
                            JsonConvert.SerializeObject(_smusettings, Formatting.Indented));
                        App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                            AppContext.BaseDirectory));
                    }

                    break;
                }
            case 'p':

                _profile = new Profile[1];
                try
                {
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                @"\SakuOverclock\profile.json");
                    Directory.CreateDirectory(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(
                        Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\profile.json",
                        JsonConvert.SerializeObject(_profile));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(),
                        AppContext.BaseDirectory));
                }

                break;
        }
    }

    //SMU Send
    private static void TryConvertToUint(string text, out uint address)
    {
        try
        {
            address = Convert.ToUInt32(text.Trim().ToLower(), 16);
        }
        catch
        {
            throw new ApplicationException("Invalid hexadecimal value.");
        }
    }

    public void Play_Invernate_QuickSMU(int mode)
    {
        SmuSettingsLoad();
        ProfileLoad();
        if (_smusettings.QuickSmuCommands == null)
        {
            return;
        }

        if (AppSettings.Preset != -1)
        {
            if (_profile[AppSettings.Preset].smuEnabled == false)
            {
                return;
            }
        }

        for (var i = 0; i < _smusettings.QuickSmuCommands.Count; i++)
        {
            if (mode == 0)
            {
                if (_smusettings.QuickSmuCommands[i].ApplyWith)
                {
                    ApplySettings(i);
                }
            }
            else
            {
                if (_smusettings.QuickSmuCommands[i].ApplyWith || _smusettings.QuickSmuCommands[i].Startup)
                {
                    ApplySettings(i);
                }
            }
        }
    }

    private void ApplySettings(int commandIndex)
    {
        try
        {
            Mailbox quickMailbox1 = new();
            SmuSettingsLoad();
            var args = Utils.MakeCmdArgs();
            var userArgs = _smusettings.QuickSmuCommands?[commandIndex].Argument.Trim().Split(',');
            TryConvertToUint(_smusettings.MailBoxes?[_smusettings.QuickSmuCommands![commandIndex].MailIndex].Cmd!,
                out var addrMsg);
            TryConvertToUint(_smusettings.MailBoxes?[_smusettings.QuickSmuCommands![commandIndex].MailIndex].Rsp!,
                out var addrRsp);
            TryConvertToUint(_smusettings.MailBoxes?[_smusettings.QuickSmuCommands![commandIndex].MailIndex].Arg!,
                out var addrArg);
            TryConvertToUint(_smusettings.QuickSmuCommands?[commandIndex].Command!, out var command);
            quickMailbox1.SMU_ADDR_MSG = addrMsg;
            quickMailbox1.SMU_ADDR_RSP = addrRsp;
            quickMailbox1.SMU_ADDR_ARG = addrArg;
            for (var i = 0; i < userArgs?.Length; i++)
            {
                if (i == args.Length)
                {
                    break;
                }

                TryConvertToUint(userArgs[i], out var temp);
                args[i] = temp;
            }

            _ = _cpu?.smu.SendSmuCommand(quickMailbox1, command, ref args);
        }
        catch (Exception ex)
        {
            LogHelper.TraceIt_TraceError(ex.ToString());
        }
    }

    //Из строк RyzenADJ в SMU Calls 
    public async void Translate(string ryzenAdjString, bool save)
    {
        try
        {
            // Список строк для проверки на совпадение, если есть - не применять, так как такое переприменение может привести к нестабильности системы
            var terminateCommands = new List<string>
        {
            "prochot-deassertion-ramp", //Безопасно переприменять
            "min-socclk-frequency",
            "max-socclk-frequency",
            "min-fclk-frequency",
            "max-fclk-frequency",
            "min-vcn",
            "max-vcn",
            "min-lclk",
            "max-lclk",
            "min-gfxclk",
            "max-gfxclk",
            "min-cpuclk",
            "max-cpuclk",
            "psi3cpu_current",
            "psi3gfx_current",
            "gfx-clk",
            "oc-clk",
            "oc-volt",
            "disable-oc", // Средне
            "enable-oc",
            "pbo-scalar",
            "psi0-current",
            "psi0soc-current",
            "enable-feature",
            "disable-feature",
            "setcpu-freqto-ramstate", // ОПАСНО ПЕРЕПРИМЕНЯТЬ
            "stopcpu-freqto-ramstate",
            "max-performance",
            "power-saving",
            "set-coall",
            "set-coper"
        };

            _saveinfo = save;
            try
            {
                SetCodeNameGeneration(); // Защита от некорректно выставленного процессора 
                // Убрать последний знак в строке аргументов для применения
                ryzenAdjString = ryzenAdjString.TrimEnd();
                // Разделить команды в Array
                var ryzenAdjCommands = ryzenAdjString.Split(' ');
                ryzenAdjCommands = [.. ryzenAdjCommands.Distinct()];
                // Выполнить через Array
                foreach (var ryzenAdjCommand in ryzenAdjCommands)
                {

                    try
                    {
                        // Проверить есть ли совпадения с листом опасных команд, которые нежелательно переприменять, чтобы не получить краш системы из-за перегрузки SMU
                        if (_checkAdjLine ==
                            ryzenAdjString /* Если прошлое применение совпадает с текущим применением */
                            && _dangersettingsapplied /* Если уже были применены опасные настройки */
                            && !save /* Если пользователь сам их не выставляет */
                            && SafeReapply /* Если включено безопасное применение */
                            && terminateCommands.Any(ryzenAdjCommand.Contains)) // Если есть совпадения в командах
                        {
                            //Ничего не делать 
                        }
                        else
                        {
                            var command = ryzenAdjCommand;
                            if (!command.Contains('='))
                            {
                                command = ryzenAdjCommand + "=0";
                            }

                            // Выяснить какая команда стоит до знака равно
                            var ryzenAdjCommandString =
                                command.Split('=')[0]
                                       .Replace("=",  null)
                                       .Replace("--", null);
                            if (command[(ryzenAdjCommand.IndexOf('=') + 1)..]
                                .Contains(',')) // Если это составная команда с не нулевым аргументом
                            {
                                var parts = command[(ryzenAdjCommand.IndexOf('=') + 1)..]
                                    .Split(','); // узнать аргументы, разделив их
                                if (parts.Length == 2 && uint.TryParse(parts[1], out var commaValue))
                                {
                                    await ApplySettings(ryzenAdjCommandString, 0x0, commaValue); // Применить преимущественно второй аргумент
                                }
                                else if (parts.Length == 3 && uint.TryParse(parts[0], out var commaValue0) 
                                                           && uint.TryParse(parts[1], out var commaValue1) 
                                                           && uint.TryParse(parts[2], out var commaValue2))
                                {
                                    await ApplySettings(ryzenAdjCommandString, commaValue0, commaValue1, commaValue2); // Применить все три аргумента
                                }
                            }
                            else
                            {
                                // А теперь узнаем что стоит после знака равно
                                var ryzenAdjCommandValueString = command[(ryzenAdjCommand.IndexOf('=') + 1)..];
                                if (ryzenAdjCommandValueString.Contains('='))
                                {
                                    ryzenAdjCommandValueString = ryzenAdjCommandValueString.Replace("=", null);
                                }

                                // Конвертировать в Uint
                                var ryzenAdjCommandValue = Convert.ToUInt32(ryzenAdjCommandValueString);

                                // Если пользователь редактировал конфиги вручную, чтобы программа не крашнулась из-за непредвиденного отрицательного значения
                                if (ryzenAdjCommandValue <= 0 && !ryzenAdjCommandString.Contains("coall") &&
                                    !ryzenAdjCommandString.Contains("coper") &&
                                    !ryzenAdjCommandString.Contains("cogfx"))
                                {
                                    await ApplySettings(ryzenAdjCommandString, 0x0); 
                                }
                                else
                                {
                                    await ApplySettings(ryzenAdjCommandString, ryzenAdjCommandValue);
                                }
                            }
                        }
                        // Добавляем задержку для новых поколений процессоров
                        if (!IsOlderGeneration())
                        {
                            await Task.Delay(130); // 130мс для новых поколений
                        }
                        else
                        {
                            await Task.Delay(50); // 50мс для старых поколений (как было)
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.TraceIt_TraceError(ex.ToString());
                    }
                }

                _dangersettingsapplied = true;
                _saveinfo = false;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
            }

            _checkAdjLine = ryzenAdjString;
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }
    private async Task ApplySettings(string commandName, uint value, uint value1 = 0, uint value2 = 0)
    {
        try
        {
            var args = new uint[6];
            args[0] = value;
            if (value1 != 0)
            {
                args[1] = value1; // Если команда составная
            }
            if (value2 != 0)
            {
                args[2] = value2;
            }

            if(Commands == null)
            {
                SetCodeNameGeneration(Codename);
            }
            // Найти код команды по имени
            var matchingCommands = Commands?.Where(c => c.Item1 == commandName);
            var commands = matchingCommands?.ToList();
            if (commands != null && commands.Count != 0)
            {
                // Реализация fallback механизма - пробуем команды по очереди до первого успеха
                var commandAppliedSuccessfully = false;
                SMU.Status? lastStatus = null; // Сохраняем последний статус для отображения

                foreach (var command in commands)
                {
                    try
                    {
                        // Применить команду и получить статус
                        var status = await ApplyThisWithStatus(command.Item2 ? 1 : 0, command.Item3, args, command.Item1);
                        lastStatus = status; // Сохраняем последний статус

                        if (status == SMU.Status.OK)
                        {
                            // Команда применилась успешно - выходим из цикла
                            commandAppliedSuccessfully = true;
                            break;
                        }
                        else
                        {
                            // Команда не применилась - ждем 20мс перед попыткой следующей
                            await Task.Delay(20);
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.TraceIt_TraceError($"Error applying command {commandName}: {ex.Message}");
                        // Продолжаем с следующей командой после задержки
                        await Task.Delay(20);
                    }
                }

                // Если ни одна команда не применилась успешно - логируем это
                if (!commandAppliedSuccessfully)
                {
                    if (lastStatus != null && lastStatus != SMU.Status.OK)
                    {
                        // Показываем последний полученный статус ошибки
                        ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                                   CommandNameParser(commandName) + "\" " +
                                                   "Param_SMU_Command_Status".GetLocalized() + " " +
                                                   StatusCommandParser(lastStatus);
                    }
                    else
                    {
                        // Если статуса нет или он OK, показываем общее сообщение о недоступности
                        ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                                   CommandNameParser(commandName) + "\" " +
                                                   "Param_SMU_Command_Unavailable".GetLocalized();
                    } 
                } 
            }
            else
            {
                if (!$"\nCommand '{commandName}' not found".Contains("Command '' not found")) // Исключаем неизвестные и пустые команды
                {
                    ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                               CommandNameParser(commandName) + "\" " +
                                               "Param_SMU_Command_Unavailable".GetLocalized();
                }
            }
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError(ex.ToString());
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                       CommandNameParser(commandName) + "\" " +
                                       "Param_SMU_Command_Unavailable".GetLocalized();
        }
    }

    private async Task<SMU.Status?> ApplyThisWithStatus(int mailbox, uint command, uint[] args, string commandName)
    {
        uint addrMsg = 0;
        uint addrRsp = 0;
        uint addrArg = 0;
        try
        {
            SetCodeNameGeneration();
            
            if (mailbox == 0)
            {
                addrMsg = RsmuCmd;
                addrRsp = RsmuRsp;
                addrArg = RsmuArg;
            }
            else
            {
                addrMsg = Mp1Cmd;
                addrRsp = Mp1Rsp;
                addrArg = Mp1Arg;
            }

            _testMailbox.SMU_ADDR_MSG = addrMsg;
            _testMailbox.SMU_ADDR_RSP = addrRsp;
            _testMailbox.SMU_ADDR_ARG = addrArg;

            if (!_saveinfo && commandName == "stopcpu-freqto-ramstate")
            {
                return SMU.Status.OK; // Пропускаем команду но возвращаем OK, для безопасности системы
            }
            if (Codename == CodeName.RavenRidge && 
                (  commandName == "min-gfxclk" || commandName == "max-gfxclk" 
                || commandName == "min-socclk-frequency" || commandName == "max-socclk-frequency" 
                || commandName == "min-fclk-frequency"   || commandName == "max-fclk-frequency" 
                || commandName == "min-vcn"  || commandName == "max-vcn" 
                || commandName == "min-lclk" || commandName == "max-lclk"))
            {
                // Mode 
                // 0 - SoC-clk
                // 1 - Fclk
                // 2 - Vcn
                // 3 - Lclk
                // 4 - Gfx-clk
                var mode = commandName.Contains("socclk") ? 0 : (
                           commandName.Contains("fclk"  ) ? 1 : (
                           commandName.Contains("vcn"   ) ? 2 : (
                           commandName.Contains("lclk"  ) ? 3 : (
                           commandName.Contains("gfx"   ) ? 4 : 3))));

                RavenSetSubsystemMinMaxFrequency(args, _cpu!, mode, commandName.Contains("max"));

                return SMU.Status.OK; // Команда применена, другим путём
            }

            var status = _cpu?.smu.SendSmuCommand(_testMailbox, command, ref args);

            return status;
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError("[SendSmuCommand]@SmuCommandsSafeApply - " + ex.ToString() + $"\nCMD: {command}.{commandName}, ARGS{args[0]}.{args[1]},MSG:{addrMsg}, ARG:{addrArg}, RSP: {addrRsp}");
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + CommandNameParser(commandName) +
                                       "Param_SMU_Command_Error".GetLocalized(); 
            return null;
        }
    }

    // Вспомогательный метод для определения старых поколений. Нужен для применения команд Smu без задержек
    private bool IsOlderGeneration()
    {
        _isOlderGeneration ??= _cpu?.info.codeName == CodeName.Picasso    || _cpuCodenameString.Contains("PICASSO") ||
                               _cpu?.info.codeName == CodeName.RavenRidge || _cpuCodenameString.Contains("RAVEN")   ||
                               _cpu?.info.codeName == CodeName.Dali       || _cpuCodenameString.Contains("DALI")    ||
                               _cpu?.info.codeName == CodeName.Cezanne    || _cpuCodenameString.Contains("CEZANNE") ||
                               _cpu?.info.codeName == CodeName.Renoir     || _cpuCodenameString.Contains("RENOIR")  ||
                               _cpu?.info.codeName == CodeName.Lucienne   || _cpuCodenameString.Contains("LUCIENNE");

        return _isOlderGeneration == true; // Использовать кешированное значение вместо постоянного прогона функций
    }

    private static string StatusCommandParser(SMU.Status? status)
    {
        return status switch
        {
            null => "\"" + "SMUErrorPlatformDesc".GetLocalized() + "\"",
            SMU.Status.CMD_REJECTED_PREREQ => "\"" + "SMUErrorPrereqDesc".GetLocalized() + "\"",
            SMU.Status.CMD_REJECTED_BUSY => "\"" + "SMUErrorBusyDesc".GetLocalized() + "\"",
            SMU.Status.FAILED => "\"" + "SMUErrorFailedDesc".GetLocalized() + "\"",
            SMU.Status.UNKNOWN_CMD => "\"" + "SMUErrorUnknownDesc".GetLocalized() + "\"",
            _ => "\"" + "SMUErrorStatusDesc".GetLocalized() + "\""
        };
    }

    private void SetCodeNameGeneration(CodeName? codeName = null)
    {
        if (codeName == null)
        {
            _cpu ??= CpuSingleton.GetInstance();
            if ((_cpu != null && Codename != _cpu.info.codeName) ||
                !_cpuCodenameString.Contains(_cpu!.info.codeName.ToString()))
            {
                Codename = _cpu.info.codeName;
                codeName = _cpu.info.codeName;
                _cpuCodenameString = Codename.ToString().ToUpper();
                _codenameGeneration = "Unknown";
            }
        }

        if (codeName == CodeName.BristolRidge)
        {
            Socket_FP4();
        }
        else if (codeName == CodeName.SummitRidge || Codename == CodeName.SummitRidge ||
            codeName == CodeName.PinnacleRidge)
        {
            Socket_AM4_V1();
        }
        else if (codeName == CodeName.RavenRidge || _cpuCodenameString.Contains("RAVEN") ||
                 codeName == CodeName.Picasso || _cpuCodenameString.Contains("PICASSO") ||
                 codeName == CodeName.Dali || _cpuCodenameString.Contains("DALI") ||
                 codeName == CodeName.FireFlight)
        {
            Socket_FP5();
        }
        else if (codeName == CodeName.Matisse ||
                 codeName == CodeName.Vermeer)
        {
            Socket_AM4_V2();
        }
        else if (codeName == CodeName.Renoir || _cpuCodenameString.Contains("RENOIR") ||
                 codeName == CodeName.Lucienne || _cpuCodenameString.Contains("LUCIENNE") ||
                 _cpuCodenameString.Contains("CEZANNE") ||
                 codeName == CodeName.Cezanne)
        {
            Socket_FP6();
        }
        else if (codeName == CodeName.VanGogh || _cpuCodenameString.Contains("VANGOGH"))
        {
            Socket_FF3();
        }
        else if (codeName == CodeName.Mendocino || _cpuCodenameString.Contains("MENDOCINO") ||
                 codeName == CodeName.Rembrandt || _cpuCodenameString.Contains("REMBRANDT") ||
                 codeName == CodeName.Phoenix || _cpuCodenameString.Contains("PHOENIX") ||
                 codeName == CodeName.Phoenix2 || _cpuCodenameString.Contains("HAWKPOINT") ||
                 codeName == CodeName.StrixPoint || _cpuCodenameString.Contains("STRIXPOINT") ||
                 codeName == CodeName.StrixHalo || codeName == CodeName.KrackanPoint)
        {
            Socket_FT6_FP7_FP8_FP11();
        }
        else if (codeName == CodeName.Raphael ||
                 codeName == CodeName.GraniteRidge ||
                 codeName == CodeName.Genoa ||
                 codeName == CodeName.StormPeak ||
                 codeName == CodeName.DragonRange ||
                 codeName == CodeName.Bergamo)
        {
            Socket_AM5_V1();
        }
        else if (codeName != null && !_unsupportedPlatformMessage)
        {
            _codenameGeneration = "Unknown";
            _unsupportedPlatformMessage = true;
            LogHelper.TraceIt_TraceError("Платформа не поддерживается. Невозможно отправить Smu команду");
        }
    }
    public string GetCodeNameGeneration(Cpu cpu)
    {
        if (_codenameGeneration == "None")
        {
            SetCodeNameGeneration(cpu.info.codeName);
        }
        return _codenameGeneration;
    }

    private string CommandNameParser(string commandName)
    {
        return commandName switch
        {
            "enable-feature" => (AppSettings.Preset > -1 && _profile[AppSettings.Preset].gpu16) ? "Param_GPU_g16/Text".GetLocalized() : "Param_SMU_Func_Text/Text".GetLocalized(),
            "disable-feature" => (AppSettings.Preset > -1 && _profile[AppSettings.Preset].gpu16) ? "Param_GPU_g16/Text".GetLocalized() : "Param_SMU_Func_Text/Text".GetLocalized(),
            "stapm-limit" => "Param_CPU_c2/Text".GetLocalized(),
            "vrm-current" => "Param_VRM_v2/Text".GetLocalized(),
            "vrmmax-current" => "Param_VRM_v1/Text".GetLocalized(),
            "tctl-temp" => "Param_CPU_c1/Text".GetLocalized(),
            "pbo-scalar" => "Param_ADV_a15/Text".GetLocalized(),
            "oc-clk" => "Param_ADV_a11/Text".GetLocalized(),
            "per-core-oc-clk" => "Param_ADV_a11/Text".GetLocalized(),
            "oc-volt" => "Param_ADV_a12/Text".GetLocalized(),
            "set-coall" => "Param_CO_O1/Text".GetLocalized(),
            "set-cogfx" => "Param_CO_O2/Text".GetLocalized(),
            "set-coper" => "Param_CCD1_CO_Section/Text".GetLocalized(),
            "enable-oc" => "Param_ADV_a14_E/Content".GetLocalized(),
            "disable-oc" => "Param_ADV_a14_E/Content".GetLocalized(),
            "stapm-time" => "Param_CPU_c5/Text".GetLocalized(),
            "fast-limit" => "Param_CPU_c3/Text".GetLocalized(),
            "slow-limit" => "Param_CPU_c4/Text".GetLocalized(),
            "slow-time" => "Param_CPU_c6/Text".GetLocalized(),
            "cHTC-temp" => "Param_CPU_c7/Text".GetLocalized(),
            "apu-skin-temp" => "Param_ADV_a6/Text".GetLocalized(),
            "vrmsoc-current" => "Param_VRM_v4/Text".GetLocalized(),
            "vrmsocmax-current" => "Param_VRM_v3/Text".GetLocalized(),
            "prochot-deassertion-ramp" => "Param_VRM_v7/Text".GetLocalized(),
            "psi3cpu_current" => "Param_ADV_a4/Text".GetLocalized(),
            "psi3gfx_current" => "Param_ADV_a5/Text".GetLocalized(),
            "gfx-clk" => "Param_ADV_a10/Text".GetLocalized(),
            "power-saving" => "Param_ADV_a13_E/Content".GetLocalized(),
            "max-performance" => "Param_ADV_a13_U/Content".GetLocalized(),
            "apu-slow-limit" => "Param_ADV_a8/Text".GetLocalized(),
            "dgpu-skin-temp" => "Param_ADV_a7/Text".GetLocalized(),
            "psi0-current" => "Param_VRM_v5/Text".GetLocalized(),
            "psi0soc-current" => "Param_VRM_v6/Text".GetLocalized(),
            "skin-temp-limit" => "Param_ADV_a9/Text".GetLocalized(),
            "max-cpuclk" => "Param_GPU_g12/Text".GetLocalized(),
            "min-cpuclk" => "Param_GPU_g11/Text".GetLocalized(),
            "max-gfxclk" => "Param_GPU_g10/Text".GetLocalized(),
            "min-gfxclk" => "Param_GPU_g9/Text".GetLocalized(),
            "max-socclk-frequency" => "Param_GPU_g2/Text".GetLocalized(),
            "min-socclk-frequency" => "Param_GPU_g1/Text".GetLocalized(),
            "max-fclk-frequency" => "Param_GPU_g4/Text".GetLocalized(),
            "min-fclk-frequency" => "Param_GPU_g3/Text".GetLocalized(),
            "max-vcn" => "Param_GPU_g6/Text".GetLocalized(),
            "min-vcn" => "Param_GPU_g5/Text".GetLocalized(),
            "max-lclk" => "Param_GPU_g8/Text".GetLocalized(),
            "min-lclk" => "Param_GPU_g7/Text".GetLocalized(),  
            "setcpu-freqto-ramstate" => "Param_GPU_g16/Text".GetLocalized(),
            "stopcpu-freqto-ramstate" => "Param_GPU_g16/Text".GetLocalized(),
            "set-gpuclockoverdrive-byvid" => "Param_ADV_a10/Text".GetLocalized(),
            _ => "1"
        };
    }

    public void CancelRange() => _cancelrange = true;

    public event EventHandler? RangeCompleted;

    public async void SendRange(string commandIndex, string startIndex, string endIndex, int mailbox, bool log)
    {
        try
        {
            _cancelrange = false;
            try
            {
                await Task.Run(() =>
                {
                    TryConvertToUint(startIndex, out var startes);
                    TryConvertToUint(endIndex, out var endes);
                    if (startes == endes)
                    {
                        startes = 0;
                        endes = uint.MaxValue;
                    }

                    var logFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                                      @"\SakuOverclock\smurangelog.txt";
                    using var sw = new StreamWriter(logFilePath, true);
                    if (log)
                    {
                        if (!File.Exists(logFilePath))
                        {
                            sw.WriteLine(@"//------SMU LOG------\\");
                        }

                        sw.WriteLine(
                            $"{DateTime.Now:HH:mm:ss} | Date: {DateTime.Now:dd.MM.yyyy} | MailBox: {mailbox} | CMD: {commandIndex} | Range: {startIndex}-{endIndex}");
                    }

                    SmuSettingsLoad();
                    for (var j = startes; j < endes; j++)
                    {
                        if (_cancelrange)
                        {
                            _cancelrange = false;
                            sw.WriteLine(@"//------CANCEL------\\");
                            RangeCompleted?.Invoke(this, EventArgs.Empty);
                            return;
                        }

                        var args = Utils.MakeCmdArgs();
                        TryConvertToUint(_smusettings?.MailBoxes![mailbox].Cmd!, out var addrMsg);
                        TryConvertToUint(_smusettings?.MailBoxes![mailbox].Rsp!, out var addrRsp);
                        TryConvertToUint(_smusettings?.MailBoxes![mailbox].Arg!, out var addrArg);
                        TryConvertToUint(commandIndex, out var command);
                        _testMailbox.SMU_ADDR_MSG = addrMsg;
                        _testMailbox.SMU_ADDR_RSP = addrRsp;
                        _testMailbox.SMU_ADDR_ARG = addrArg;
                        args[0] = j;
                        try
                        {
                            var status = _cpu?.smu.SendSmuCommand(_testMailbox, command, ref args);
                            if (log)
                            {
                                sw.WriteLine(
                                    $"{DateTime.Now:HH:mm:ss} | MailBox: {mailbox} | CMD: {command:X} | Arg: {j:X} | Status: {status} | Output HEX: {args[0]:X2} | Output DEC: {args[0]}");
                            }
                        }
                        catch (Exception ex)
                        {
                            if (log)
                            {
                                sw.WriteLine(
                                    $"{DateTime.Now:HH:mm:ss} | MailBox: {mailbox} | CMD: {command:X} | Arg: {j:X} | Status: {ex.Message} | Output: {args[0]:X2} | Output: {args[0]}");
                            }
                        }
                    }

                    if (log)
                    {
                        RangeCompleted?.Invoke(this, EventArgs.Empty);
                        sw.WriteLine(@"//------OK------\\");
                    }
                });
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e.ToString());
        }
    }

    public uint ReturnCoGfx(CodeName codeName, bool isMp1)
    {
        Codename = codeName; //если класс не инициализирован - задать правильный Codename
        SetCodeNameGeneration(codeName);

        var matchingCommands = Commands?.Where(c => c.Item1 == "set-cogfx" && c.Item2 == isMp1);
        var commands = matchingCommands!.ToList();
        if (commands.Count != 0)
        {
            return commands.Select(command => command.Item3).FirstOrDefault();
        }

        return 0U;
    }

    public uint ReturnCoPer(CodeName codeName, bool isMp1)
    {
        Codename = codeName; //если класс не инициализирован - задать правильный Codename
        SetCodeNameGeneration(codeName);

        var matchingCommands = Commands?.Where(c => c.Item1 == "set-coper" && c.Item2 == isMp1);
        var commands = matchingCommands!.ToList();
        if (commands.Count != 0)
        {
            return commands.Select(command => command.Item3).FirstOrDefault();
        }

        return 0U;
    }

    public double ReturnCpuPowerLimit(Cpu cpu)
    {
        Codename = cpu.info.codeName; //если класс не инициализирован - задать правильный Codename
        SetCodeNameGeneration(cpu.info.codeName);

        var actualCommand = 0x0U;
        var matchingCommandsMp1 = Commands?.Where(c => c.Item1 == "get-sustained-power-and-thm-limit" && c.Item2 == true);
        var matchingCommandsRsmu = Commands?.Where(c => c.Item1 == "get-sustained-power-and-thm-limit" && c.Item2 == false);
        var commands = matchingCommandsMp1!.ToList();
        var commands1 = matchingCommandsRsmu!.ToList();
        var mailbox = 1;
        if (commands.Count != 0)
        {
            actualCommand = commands.Select(command => command.Item3).FirstOrDefault();
            mailbox = 1; // MP1
        }
        if (commands1.Count != 0)
        {
            actualCommand = commands1.Select(command => command.Item3).FirstOrDefault();
            mailbox = 0; // RSMU
        }

        if ((commands.Count != 0 || commands1.Count != 0) && actualCommand != 0x0U)
        {
            uint addrMsg;
            uint addrRsp;
            uint addrArg;

            if (mailbox == 0)
            {
                addrMsg = RsmuCmd;
                addrRsp = RsmuRsp;
                addrArg = RsmuArg;
            }
            else
            {
                addrMsg = Mp1Cmd;
                addrRsp = Mp1Rsp;
                addrArg = Mp1Arg;
            }

            Mailbox testMailbox1 = new()
            {
                SMU_ADDR_MSG = addrMsg,
                SMU_ADDR_RSP = addrRsp,
                SMU_ADDR_ARG = addrArg
            };

            var args = Utils.MakeCmdArgs();
            var status = cpu?.smu.SendSmuCommand(testMailbox1, actualCommand, ref args);

            if (status != SMU.Status.OK)
            {
                LogHelper.TraceIt_TraceError("[SendSmuCommand+OCFinder]@Unable_To_Get_CpuPowerLimit_From_Smu_STATUS - " + status);
            }

            if (args[0] != 0x0)
            {
                return (args[0] & 0x00FF0000) >> 16;
            }
        }

        return 15;
    }
    public bool ReturnUndervoltingAvailability(Cpu cpu)
    {
        Codename = cpu.info.codeName; //если класс не инициализирован - задать правильный Codename
        SetCodeNameGeneration(cpu.info.codeName);

        var actualCommand = 0x0U;
        var matchingCommandsMp1 = Commands?.Where(c => c.Item1 == "get-coper-options" && c.Item2 == true);
        var matchingCommandsRsmu = Commands?.Where(c => c.Item1 == "get-coper-options" && c.Item2 == false);
        var commands = matchingCommandsMp1!.ToList();
        var commands1 = matchingCommandsRsmu!.ToList();
        var mailbox = 1;
        if (commands.Count != 0)
        {
            actualCommand = commands.Select(command => command.Item3).FirstOrDefault();
            mailbox = 1; // MP1
        }
        if (commands1.Count != 0)
        {
            actualCommand = commands1.Select(command => command.Item3).FirstOrDefault();
            mailbox = 0; // RSMU
        }

        if ((commands.Count != 0 || commands1.Count != 0) && actualCommand != 0x0U)
        {
            uint addrMsg;
            uint addrRsp;
            uint addrArg;

            if (mailbox == 0)
            {
                addrMsg = RsmuCmd;
                addrRsp = RsmuRsp;
                addrArg = RsmuArg;
            }
            else
            {
                addrMsg = Mp1Cmd;
                addrRsp = Mp1Rsp;
                addrArg = Mp1Arg;
            }

            Mailbox testMailbox1 = new()
            {
                SMU_ADDR_MSG = addrMsg,
                SMU_ADDR_RSP = addrRsp,
                SMU_ADDR_ARG = addrArg
            };

            var args = Utils.MakeCmdArgs();
            var status = cpu?.smu.SendSmuCommand(testMailbox1, actualCommand, ref args);

            if (status != SMU.Status.OK)
            {
                LogHelper.TraceIt_TraceError("[SendSmuCommand+OCFinder]@Unable_To_Get_CoperUndervolting_STATUS - " + status);
            }

            if (args[0] != 0x0)
            {
                return true;
            }
            else
            {
                return TrySetUndervolt(cpu);
            }
        }

        if (Codename == CodeName.RavenRidge || Codename == CodeName.FireFlight || Codename == CodeName.Dali || Codename == CodeName.Picasso)
        {
            return true;
        }
        else
        {
            return TrySetUndervolt(cpu);
        }
    }
    private static bool TrySetUndervolt(Cpu? cpu)
    {
        var actualCommand = 0x0U;
        var matchingCommandsMp1 = Commands?.Where(c => c.Item1 == "set-coall" && c.Item2 == true);
        var matchingCommandsRsmu = Commands?.Where(c => c.Item1 == "set-coall" && c.Item2 == false);
        var commands = matchingCommandsMp1!.ToList();
        var commands1 = matchingCommandsRsmu!.ToList();
        var mailbox = 1;
        if (commands.Count != 0)
        {
            actualCommand = commands.Select(command => command.Item3).FirstOrDefault();
            mailbox = 1; // MP1
        }
        if (commands1.Count != 0)
        {
            actualCommand = commands1.Select(command => command.Item3).FirstOrDefault();
            mailbox = 0; // RSMU
        }

        if ((commands.Count != 0 || commands1.Count != 0) && actualCommand != 0x0U)
        {
            uint addrMsg;
            uint addrRsp;
            uint addrArg;

            if (mailbox == 0)
            {
                addrMsg = RsmuCmd;
                addrRsp = RsmuRsp;
                addrArg = RsmuArg;
            }
            else
            {
                addrMsg = Mp1Cmd;
                addrRsp = Mp1Rsp;
                addrArg = Mp1Arg;
            }

            Mailbox testMailbox1 = new()
            {
                SMU_ADDR_MSG = addrMsg,
                SMU_ADDR_RSP = addrRsp,
                SMU_ADDR_ARG = addrArg
            };

            var args = Utils.MakeCmdArgs();
            var status = cpu?.smu.SendSmuCommand(testMailbox1, actualCommand, ref args);

            if (status != SMU.Status.OK)
            {
                LogHelper.TraceIt_TraceError("[SendSmuCommand+OCFinder]@Unable_To_Set_CurveOptimizerUndervolting_STATUS - " + status);
                return false;
            }
            else 
            {
                return true;
            }
        }

        return false;
    }

    public bool? IsPlatformPC(Cpu cpu)
    {
        Codename = cpu.info.codeName;
        if (IsPlatformPCByCodename(Codename) == true)
        {
            if (Codename is CodeName.RavenRidge or CodeName.Picasso or CodeName.Renoir or CodeName.Cezanne or CodeName.Phoenix or CodeName.Phoenix2)
            {
                if (cpu.info.packageType == PackageType.FPX)
                {
                    if (_isBatteryUnavailable)
                    {
                        if (cpu.info.cpuName.Contains('G') ||
                            cpu.info.cpuName.Contains("GE") ||
                            (cpu.info.cpuName.Contains('X') && !cpu.info.cpuName.Contains("HX")) ||
                            cpu.info.cpuName.Contains('F') ||
                            (cpu.info.cpuName.Contains("X3D") && !cpu.info.cpuName.Contains("HX3D")) ||
                            cpu.info.cpuName.Contains("XT")
                           )
                        {
                            return true;
                        }

                        return false;
                    }
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return true;
        }
        return null; // Платформа не определена!
    }
    private static bool? IsPlatformPCByCodename(CodeName codeName)
    {
        if (Codename != codeName)
        {
            Codename = codeName;
        }
        return Codename switch
        {
            CodeName.BristolRidge or CodeName.SummitRidge or CodeName.PinnacleRidge => true,
            CodeName.RavenRidge or CodeName.Picasso or CodeName.Dali or CodeName.FireFlight => false, // Raven Ridge, Picasso can be PC!
            CodeName.Matisse or CodeName.Vermeer => true,
            CodeName.Renoir or CodeName.Lucienne or CodeName.Cezanne => false, // Renoir, Cezanne can be PC!
            CodeName.VanGogh => false,
            CodeName.Mendocino or CodeName.Rembrandt or CodeName.Phoenix or CodeName.Phoenix2 or CodeName.HawkPoint or CodeName.StrixPoint or CodeName.StrixHalo => false, // Phoenix can be PC!
            CodeName.GraniteRidge or CodeName.Genoa or CodeName.Bergamo or CodeName.Raphael or CodeName.DragonRange => true,
            _ => null,// Устройство не определено
        };
    }

    public uint GenerateSmuArgForSetGfxclkOverdriveByFreqVid(double frequencyMHz, double voltage)
    {
        // Вычисляем VID по напряжению
        var rawVid = (1.55 - voltage) / 0.00625;
        var vid = (uint)Math.Round(rawVid);

        // Ограничения по диапазону VID
        if (vid > 0xFF)
        {
            vid = 0xFF;
        }

        // Конвертируем частоту в целочисленный формат
        var freq = (uint)Math.Round(frequencyMHz);

        // Смещаем и комбинируем
        var smuArg = (vid << 16) | (freq & 0xFFFF);
        return smuArg;
    }

    private void RavenSetSubsystemMinMaxFrequency(uint[] args, Cpu cpu, int mode = 0, bool setMax = false)
    {
        // Mode 
        // 0 - SoC-clk
        // 1 - Fclk
        // 2 - Vcn
        // 3 - Lclk
        // 4 - Gfx-clk
        Codename = cpu.info.codeName; //если класс не инициализирован - задать правильный Codename
        SetCodeNameGeneration(cpu.info.codeName);

        Mailbox amdGpuMailbox = new()
        {
            SMU_ADDR_MSG = 0x3B10A08,
            SMU_ADDR_RSP = 0x3B10A68,
            SMU_ADDR_ARG = 0x3B10A48
        };
        var command = mode switch
        {
            0 => setMax ? 0x32u : 0x21u,
            1 => setMax ? 0x33u : 0x12u,
            2 => setMax ? 0x34u : 0x28u,
            3 => setMax ? 0x14u : 0x23u,
            4 => setMax ? 0x30u : 0x31u,
            _ => setMax ? 0x30u : 0x31u,
        };

        var status = cpu?.smu.SendSmuCommand(amdGpuMailbox, command, ref args);

        if (status != SMU.Status.OK)
        {
            LogHelper.TraceIt_TraceError("[SendSmuCommand+RavenCommands]@Raven_SetGpuMinMaxFrequency_StatusNotOK: - " + status);
        }
    }


    /*
     * SMU Command Set - Reverse-Engineered
     *
     * This collection of SMU (System Management Unit) commands
     * was manually discovered and verified through extensive testing on real AMD Ryzen hardware.
     *
     * No official documentation was available during development. Every single command here
     * was identified using a trial-and-error approach across multiple Ryzen-powered laptops,
     * often with nothing more than educated guesses, pattern analysis, and persistence.
     *
     * This work represents hours of low-level experimentation — poking registers, observing changes,
     * cross-checking behaviors across firmware and silicon revisions, and confirming results
     * empirically in the field.
     *
     * These commands are not theoretical — they're proven to work in real scenarios.
     * Use with care, and always validate on your target hardware.
     *
     * — @Serzhil Sakurajima (2025)
     */

    private static void Socket_FP4()
    {
        _codenameGeneration = "FP4";

        Mp1Cmd = 0x13000000;
        Mp1Rsp = 0x13000010;
        Mp1Arg = 0x13000020;

        Commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: A12-9720P, FX-9830P
            // SMU command map for FP4 socket (last update: 2025-07-09)

/*   Smu  */("enable-feature",                    true,  0x5F), // Use with caution!
/*Features*/("disable-feature",                   true,  0x60),

            ("stapm-limit",                       true,  0x6c), // Args: Power [mW], mode [0 - system config, 1 - boost disabled, 2 - boost enabled], stapm-time
            ("fast-limit",                        true,  0x69),
            ("slow-limit",                        true,  0x6a), // Args: Power AC [mW], Power DC [mW] use with caution!
            ("tctl-temp",                         true,  0x7c),
/*  DPTC  */("vrm-current",                       true,  0x67), // Args: Cpu core, Nb, Gfx
            ("vrmmax-current",                    true,  0x6b), 
            ("psi0-current",                      true,  0x82), // Args: Cpu core, Nb, Gfx use with caution!
            ("prochot-deassertion-ramp",          true,  0x81), // Min value 0 max value 100

/* Power  */("power-saving",                      true,  0x62),
/* Saving */("max-performance",                   true,  0x61),

/*   OC   */("oc-volt",                           true,  0x88), // Command not found when applied but have in documentation
/* Options*/

/*Subsystm*/("max-lclk",                          true,  0x4f), // Danger! use with caution!
/* Clocks */("min-lclk",                          true,  0x4e), // Danger! use with caution!
            
/*  AcBtc */("setcpu-freqto-ramstate",            true,  0x77), // Start BTC ."Failed" status when apply
        ];
    }

    private static void Socket_FP5()
    {
        _codenameGeneration = "FP5";

        Mp1Cmd = 0x3B10528;
        Mp1Rsp = 0x3B10564;
        Mp1Arg = 0x3B10998;

        RsmuCmd = 0x3B10A20;
        RsmuRsp = 0x3B10A80;
        RsmuArg = 0x3B10A88;

        Commands =
        [
            // Store the commands
            // true - Use MP1 address
            // false - Use RSMU address
            // Those commands are 100% tested
            // Tested on: Ryzen 3 2200U, Ryzen 5 3200U, Ryzen 5 3500U
            // SMU command map for FP5 socket (last update: 2025-07-05)

/*   Smu  */("enable-feature",                    true,  0x05), 
/*Features*/("disable-feature",                   true,  0x06),

            ("stapm-limit",                       true,  0x1a),
            ("stapm-limit",                       false, 0x2e),
            ("stapm-time",                        true,  0x1e),
            ("stapm-time",                        false, 0x32),
            ("fast-limit",                        true,  0x1b),
            ("fast-limit",                        false, 0x30),
            ("slow-limit",                        true,  0x1c),
            ("slow-limit",                        false, 0x2f),
            ("slow-time",                         true,  0x1d),
            ("slow-time",                         false, 0x31),
            ("tctl-temp",                         true,  0x1f),
            ("tctl-temp",                         false, 0x33),
/*  DPTC  */("cHTC-temp",                         false, 0x56), // Not sure, rejected
            ("vrm-current",                       true,  0x20),
            ("vrm-current",                       false, 0x34),
            ("vrmmax-current",                    true,  0x22),
            ("vrmmax-current",                    false, 0x36),
            ("vrmsoc-current",                    true,  0x21),
            ("vrmsoc-current",                    false, 0x35),
            ("vrmsocmax-current",                 true,  0x23),
            ("vrmsocmax-current",                 false, 0x37),
            ("psi0-current",                      true,  0x24),
            ("psi0-current",                      false, 0x38),
            ("psi0soc-current",                   true,  0x25),
            ("psi0soc-current",                   false, 0x39),
            ("prochot-deassertion-ramp",          true,  0x26),
            ("prochot-deassertion-ramp",          false, 0x3a),

/* Power  */("power-saving",                      true,  0x19),
/* Saving */("max-performance",                   true,  0x18),

            ("enable-oc",                         true,  0x58),
            ("disable-oc",                        true,  0x3f), // Require 0x1 in args
            ("oc-clk",                            false, 0x7d), // Not sure, "Rejected" status when apply
            ("per-core-oc-clk",                   false, 0x7e), // Not sure, "Rejected" 
            ("oc-clk",                            true,  0x59), // "Failed" status when apply, maybe blocked by condition, maybe need to enable OC Mode (can't do it in usual way)
/*   OC   */("per-core-oc-clk",                   true,  0x5a), // "Failed" status when apply
/* Options*/("oc-clk",                            true,  0x41), // Old AMD CBS OC method, freq in MHz, required MP1 entertain OC Mode (#define BIOSSMC_MSG_OC_Disable 3F /*Arg 0x1 - disable OC Mode, arg 0x0 - enable OC Mode*/)
            ("oc-volt",                           true,  0x5b), // "Failed" status when apply
            ("oc-volt",                           false, 0x7c), // Not sure, "Rejected"
            ("oc-volt",                           true,  0x40), // Old AMD CBS OC method, set max VID, required MP1 entertain OC Mode
            ("set-gpuclockoverdrive-byvid",       true,  0x3d), // Old AMD CBS OC method, required MP1 entertain OC Mode, this command set overclocked iGPU freq and voltage
            ("set-gpuclockoverdrive-byvid",       false, 0x61),
            ("pbo-scalar",                        true,  0x57), // "Failed" status when apply
            ("pbo-scalar",                        false, 0x63), // Not sure, need to retest
            ("get-pbo-scalar",                    false, 0x62),

            ("max-cpuclk",                        true,  0x44),
            ("min-cpuclk",                        true,  0x45),
            ("max-gfxclk",                        true,  0x46),
            ("max-gfxclk",                        false, 0x68),
            ("min-gfxclk",                        true,  0x47),
            ("min-gfxclk",                        false, 0x69),
            ("max-socclk-frequency",              true,  0x48),
            ("max-socclk-frequency",              false, 0x66),
/*Subsystm*/("min-socclk-frequency",              true,  0x49),
/* Clocks */("min-socclk-frequency",              false, 0x67),
            ("max-fclk-frequency",                true,  0x4a),
            ("min-fclk-frequency",                true,  0x4b),
            ("max-vcn",                           true,  0x4c),
            ("min-vcn",                           true,  0x4d),
            ("max-lclk",                          true,  0x4e),
            ("min-lclk",                          true,  0x4f),

/*  Curve */("set-coper",                         false, 0x58),
/*Optimizr*/("set-coall",                         false, 0x59),
            ("set-cogfx",                         false, 0x59), // Cuz Raven, Dali and Picasso have gfx voltage control in this command too but in different registers
            
            ("setcpu-freqto-ramstate",            true,  0x2f),
/*  AcBtc */("stopcpu-freqto-ramstate",           true,  0x30),
            ("stopcpu-freqto-ramstate",           true,  0x31),

            ("set-ulv-vid",                       true,  0x35), // Experimental. Set ULV voltage for CPU sleep state. Can be high, there are no limits. Higher values can cause degradation!
            ("set-vddoff-vid",                    true,  0x3a), // System voltage offset when CPUOFF or GFXOFF state is triggered
            ("set-vmin-freq",                     true,  0x3b), // GFX minimum Curve Optimizer range 
            ("get-sustained-power-and-thm-limit", true,  0x43),
            ("get-sustained-power-and-thm-limit", false, 0x65),
/*  Debug */("get-pbo-fused-power-limit",         false, 0x7F), // Fused STAPM limit
            ("get-pbo-fused-slow-limit",          false, 0x80),
            ("get-pbo-fused-fast-limit",          false, 0x81),
            ("get-pbo-fused-apu-slow-limit",      false, 0x82),
            ("get-pbo-fused-vrmtdc-limit",        false, 0x83),
            ("get-pbo-fused-vrmsoc-current",      false, 0x84)
        ];
    }

    private static void Socket_FP6()
    {
        _codenameGeneration = "FP6";

        Mp1Cmd = 0x3B10528;
        Mp1Rsp = 0x3B10564;
        Mp1Arg = 0x3B10998;

        RsmuCmd = 0x3B10A20;
        RsmuRsp = 0x3B10A80;
        RsmuArg = 0x3B10A88;

        Commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: Ryzen 5 5600H
            // SMU command map for FP6 socket (last update: 2025-07-05)

/*  Smu   */("enable-feature",                    true,  0x05), 
/*Features*/("disable-feature",                   true,  0x07),

            ("stapm-limit",                       true,  0x14),
            ("stapm-limit",                       false, 0x31),
            ("stapm-time",                        true,  0x18),
            ("stapm-time",                        false, 0x36),
            ("fast-limit",                        true,  0x15),
            ("fast-limit",                        false, 0x32),
            ("slow-limit",                        true,  0x16),
            ("slow-limit",                        false, 0x33),
            ("slow-time",                         true,  0x17),
            ("slow-time",                         false, 0x35),
            ("tctl-temp",                         true,  0x19),
            ("cHTC-temp",                         false, 0x37), // Not sure, accepted but nothing changed
/*  DPTC  */("vrm-current",                       true,  0x1a),
            ("vrm-current",                       false, 0x38),
            ("vrmmax-current",                    true,  0x1c),
            ("vrmmax-current",                    false, 0x3a),
            ("vrmsoc-current",                    true,  0x1b),
            ("vrmsoc-current",                    false, 0x39),
            ("vrmsocmax-current",                 true,  0x1d),
            ("vrmsocmax-current",                 false, 0x3b),
            ("psi0-current",                      true,  0x1e),
            ("psi0-current",                      false, 0x3c),
            ("psi0soc-current",                   true,  0x1f),
            ("psi0soc-current",                   false, 0x3d),
            ("prochot-deassertion-ramp",          true,  0x20),
            ("prochot-deassertion-ramp",          false, 0x3e),

            ("skin-temp-limit",                   true,  0x53), // Use instead of STAPM
            ("apu-slow-limit",                    true,  0x21),
            ("apu-slow-limit",                    false, 0x34),
/*  Stt   */("apu-skin-temp",                     true,  0x38),
/* Limits */("apu-skin-temp",                     false, 0x91),
            ("dgpu-skin-temp",                    true,  0x39),
            ("dgpu-skin-temp",                    false, 0x92),

/* Power  */("max-performance",                   true,  0x11),
/* Saving */("power-saving",                      true,  0x12),

            ("enable-oc",                         true,  0x2f),
            ("enable-oc",                         false, 0x17),
            ("disable-oc",                        true,  0x30),
            ("disable-oc",                        false, 0x18),
            ("oc-clk",                            true,  0x31),
            ("oc-clk",                            false, 0x19),
/*   OC   */("per-core-oc-clk",                   true,  0x32),
/* Options*/("per-core-oc-clk",                   false, 0x1a),
            ("oc-volt",                           true,  0x33),
            ("oc-volt",                           false, 0x1b),
            ("set-gpuclockoverdrive-byvid",       true,  0x34),
            ("gfx-clk",                           false, 0x89),
            ("gfx-clk",                           false, 0x1c),
            ("pbo-scalar",                        true,  0x49), // Not sure
            ("pbo-scalar",                        false, 0x3f),
            ("get-pbo-scalar",                    false, 0x0f),

            ("set-cogfx",                         false, 0x53), // "Failed" status when apply, maybe blocked by condition, maybe need to enable OC Mode (can't do it in usual way)
            ("set-coper",                         true,  0x54), // "Failed" status when apply
/*  Curve */("set-coper",                         false, 0x52), // "Failed" status when apply
/*Optimizr*/("set-coall",                         true,  0x55), // "Failed" status when apply
            ("set-coall",                         false, 0xB1), // "Failed" status when apply
            ("get-coper-options",                 false, 0xC3),
            ("get-cogfx-options",                 false, 0xC6),

            ("get-sustained-power-and-thm-limit", true,  0x5b), // Seems no equal command in RSMU
            ("get-pbo-fused-power-limit",         false, 0x11), // Fused STT limit
            ("get-pbo-fused-slow-limit",          false, 0x12),
/*  Debug */("get-pbo-fused-fast-limit",          false, 0x13),
            ("get-pbo-fused-apu-slow-limit",      false, 0x14),
            ("get-pbo-fused-vrmtdc-limit",        false, 0x15),
            ("get-pbo-fused-vrmsoc-current",      false, 0x16),
        ];
    }

    private static void Socket_FF3()
    {
        _codenameGeneration = "FF3";

        Mp1Cmd = 0x3B10528;
        Mp1Rsp = 0x3B10578;
        Mp1Arg = 0x3B10998;

        RsmuCmd = 0x3B10a20;
        RsmuRsp = 0x3B10a80;
        RsmuArg = 0x3B10a88;

        Commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: AMD Custom APU 0405
            // SMU command map for FF3 socket (last update: 2025-07-05)

/*  Smu   */("enable-feature",                    true,  0x05), 
/*Features*/("disable-feature",                   true,  0x07),

            ("stapm-limit",                       true,  0x14),
            ("stapm-limit",                       false, 0x31),
            ("stapm-time",                        true,  0x18),
            ("fast-limit",                        true,  0x15),
            ("slow-limit",                        true,  0x16),
            ("slow-time",                         true,  0x17),
            ("tctl-temp",                         true,  0x19),
            ("cHTC-temp",                         false, 0x37), // Not sure
/*  DPTC  */("vrm-current",                       true,  0x1a),
            ("vrmmax-current",                    true,  0x1c),
            ("vrmsoc-current",                    true,  0x1b),
            ("vrmsocmax-current",                 true,  0x1d),
            ("psi0-current",                      true,  0x1e),
            ("psi0soc-current",                   true,  0x1f),
            ("psi3cpu_current",                   true,  0x20),
            ("psi3gfx_current",                   true,  0x21),
            ("prochot-deassertion-ramp",          true,  0x22),

            ("skin-temp-limit",                   true,  0x4a), // Use instead of STAPM
/*  Stt   */("apu-slow-limit",                    true,  0x23),
/* Limits */("apu-skin-temp",                     true,  0x33),

/* Power  */("max-performance",                   true,  0x11),
/* Saving */("power-saving",                      true,  0x12),

/*   OC   */("gfx-clk",                           false, 0x89), // Not sure
/* Options*/("gfx-clk",                           false, 0x1c), // Not sure

            ("set-cogfx",                         false, 0xb7), 
/*  Curve */("set-coper",                         true,  0x4b), 
/*Optimizr*/("set-coall",                         true,  0x4c),
            ("set-coall",                         false, 0x5d), 

/*  Debug */("get-sustained-power-and-thm-limit", true,  0x54)

        ];
    }

    private static void Socket_FT6_FP7_FP8_FP11()
    {
        if (Codename == CodeName.StrixPoint || Codename == CodeName.StrixHalo)
        {
            _codenameGeneration = "FP8";
            
            Mp1Cmd = 0x3B10928;
            Mp1Rsp = 0x3B10978;
            Mp1Arg = 0x3B10998;
        }
        else
        {
            _codenameGeneration = "FP7";

            Mp1Cmd = 0x3B10528;
            Mp1Rsp = 0x3B10578;
            Mp1Arg = 0x3B10998;
        }

        RsmuCmd = 0x3B10a20;
        RsmuRsp = 0x3B10a80;
        RsmuArg = 0x3B10a88;

        Commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: Ryzen 3 8440U, Ryzen Z1 Extreme
            // SMU command map for FT6, FP7, FP8, FP11 socket (last update: 2025-07-05)

/*  Smu   */("enable-feature",                    true,  0x05), 
/*Features*/("disable-feature",                   true,  0x07),

            ("stapm-limit",                       true,  0x14),
            ("stapm-limit",                       false, 0x31),
            ("stapm-time",                        true,  0x18),
            ("stapm-time",                        false, 0x36),
            ("fast-limit",                        true,  0x15),
            ("fast-limit",                        false, 0x32),
            ("slow-limit",                        true,  0x16),
            ("slow-limit",                        false, 0x33),
            ("slow-time",                         true,  0x17),
            ("slow-time",                         false, 0x35),
            ("tctl-temp",                         true,  0x19),
            ("cHTC-temp",                         true,  0x63),
            ("cHTC-temp",                         false, 0x37), // Not sure, accepted but nothing changed
/*  DPTC  */("vrm-current",                       true,  0x1a),
            ("vrm-current",                       false, 0x38),
            ("vrmmax-current",                    true,  0x1c),
            ("vrmmax-current",                    false, 0x3a),
            ("vrmsoc-current",                    true,  0x1b),
            ("vrmsoc-current",                    false, 0x39),
            ("vrmsocmax-current",                 true,  0x1d),
            ("vrmsocmax-current",                 false, 0x3b),
            ("psi0-current",                      true,  0x1e),
            ("psi0-current",                      false, 0x3c),
            ("psi0soc-current",                   true,  0x1f),
            ("psi0soc-current",                   false, 0x3d),
            ("psi3cpu_current",                   true,  0x20),
            ("psi3gfx_current",                   true,  0x21),
            ("prochot-deassertion-ramp",          true,  0x22), 

            ("skin-temp-limit",                   true,  0x4a), // Use instead of STAPM
            ("apu-slow-limit",                    true,  0x23),
            ("apu-slow-limit",                    false, 0x34),
/*  Stt   */("apu-skin-temp",                     true,  0x33),
/* Limits */("apu-skin-temp",                     false, 0x91),
            ("dgpu-skin-temp",                    true,  0x34),
            ("dgpu-skin-temp",                    false, 0x92),

/* Power  */("max-performance",                   true,  0x11),
/* Saving */("power-saving",                      true,  0x12),

            ("enable-oc",                         false, 0x17),
            ("disable-oc",                        false, 0x18),
            ("oc-clk",                            false, 0x19),
/*   OC   */("per-core-oc-clk",                   false, 0x1a),
/* Options*/("oc-volt",                           false, 0x1b),
            ("gfx-clk",                           false, 0x89),
            ("gfx-clk",                           false, 0x1c),
            ("pbo-scalar",                        false, 0x3e),
            ("get-pbo-scalar",                    false, 0x0f),

            ("set-cogfx",                         false, 0xb7), // Not sure, "Rejected" status when apply
            ("set-coper",                         true,  0x4b), // "Failed" status when apply, maybe blocked by condition, maybe need to enable OC Mode (can't do it in usual way)
/*  Curve */("set-coper",                         false, 0x53),
/*Optimizr*/("set-coall",                         true,  0x4c), // "Failed" status when apply
            ("set-coall",                         false, 0x5d),
            ("get-coper-options",                 false, 0xE1),

            ("get-sustained-power-and-thm-limit", true,  0x5f), // Seems no equal command in RSMU
            ("get-pbo-fused-power-limit",         false, 0x11), // Fused STT limit
            ("get-pbo-fused-slow-limit",          false, 0x12),
/*  Debug */("get-pbo-fused-fast-limit",          false, 0x13),
            ("get-pbo-fused-apu-slow-limit",      false, 0x14),
            ("get-pbo-fused-vrmtdc-limit",        false, 0x15),
            ("get-pbo-fused-vrmsoc-current",      false, 0x16),
            ("get-pbo-fused-tctl-temp",           false, 0xE5)

        ];
    }

    private static void Socket_AM4_V1()
    {
        _codenameGeneration = "AM4_V1";

        Mp1Cmd = 0X3B10528;
        Mp1Rsp = 0X3B10564;
        Mp1Arg = 0X3B10598;

        RsmuCmd = 0x3B1051C;
        RsmuRsp = 0X3B10568;
        RsmuArg = 0X3B10590;

        Commands =
        [
            // Store the commands
            ("fast-limit",      false, 0x64), // PPT limit
            ("vrm-current",     false, 0x65),
            ("vrmmax-current",  false, 0x66),
            ("tctl-temp",       false, 0x68), 
/*   OC   */("pbo-scalar",      false, 0x6a),
/* Options*/("oc-clk",          false, 0x6c),
            ("per-core-oc-clk", false, 0x6d),
            ("oc-volt",         false, 0x6e),
            ("enable-oc",       true,  0x23),
            ("enable-oc",       false, 0x6b),
            ("disable-oc",      true,  0x24)
        ];
    }

    private static void Socket_AM4_V2()
    {
        _codenameGeneration = "AM4_V2";

        Mp1Cmd = 0x3B10530;
        Mp1Rsp = 0x3B1057C;
        Mp1Arg = 0x3B109C4;

        RsmuCmd = 0x3B10524;
        RsmuRsp = 0x3B10570;
        RsmuArg = 0x3B10A40;

        Commands =
        [
            // Store the commands
            ("enable-feature",                    true,  0x03),
            ("disable-feature",                   true,  0x04), // Can be locked

            ("fast-limit",                        true,  0x3d), // PPT limit
            ("fast-limit",                        false, 0x53),
            ("vrm-current",                       true,  0x3b),
            ("vrm-current",                       false, 0x54),
            ("vrmmax-current",                    true,  0x3c),
            ("vrmmax-current",                    false, 0x55),
            ("tctl-temp",                         true,  0x3e),
            ("cHTC-temp",                         false, 0x56),
            ("pbo-scalar",                        false, 0x58),
            ("get-pbo-scalar",                    false, 0x6c),
            ("oc-clk",                            true,  0x26),
            ("oc-clk",                            false, 0x5c),
            ("set-boost-limit-frequency",         true,  0x2b),
/*   OC   */("per-core-oc-clk",                   true,  0x27),
/* Options*/("per-core-oc-clk",                   false, 0x5d),
            ("oc-volt",                           true,  0x28),
            ("oc-volt",                           false, 0x61),
            ("set-coall",                         true,  0x36),
            ("set-coper",                         true,  0x35),
            ("set-coall",                         false, 0x0b),
            ("set-coper",                         false, 0x0a),
            ("enable-oc",                         true,  0x24),
            ("enable-oc",                         false, 0x5a),
            ("disable-oc",                        true,  0x25),
            ("disable-oc",                        false, 0x5b),
            ("get-sustained-power-and-thm-limit", true,  0x23),
            ("get-overclocking-support",          false, 0x6f),
            ("get-coper-options",                 false, 0x7c)
        ];
    }

    private static void Socket_AM5_V1()
    {
        _codenameGeneration = "AM5";

        Mp1Cmd = 0x3B10530;
        Mp1Rsp = 0x3B1057C;
        Mp1Arg = 0x3B109C4;

        RsmuCmd = 0x3B10524;
        RsmuRsp = 0x3B10570;
        RsmuArg = 0x3B10A40;

        Commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: Ryzen 9 7900X3D
            // SMU command map for AM5 socket (last update: 2025-07-05)
            ("enable-feature",                    true,  0x03),
            ("disable-feature",                   true,  0x04), // Can be locked
            //("stapm-limit",                     true,  0x4f), // Set CPU Stapm value! Not affect on PPT. Works only if Stapm feature is enabled on platform BE CAREFUL! If you apply this command and then "fast-limit" - actual PPT limit will NOT be saved thats why I commented that command for safety
            ("fast-limit",                        true,  0x3e),
            ("fast-limit",                        false, 0x56), // Set CPU PPT Limit
            ("slow-limit",                        true,  0x5f),
            ("slow-limit",                        false, 0xcb),
            ("skin-temp-limit",                   true,  0x5e),
            ("stapm-time",                        true,  0x4e),
            ("slow-time",                         true,  0x61),
            ("apu-slow-limit",                    true,  0x60),
            ("vrm-current",                       true,  0x3c),
            ("vrm-current",                       false, 0x57),
            ("vrmmax-current",                    true,  0x3d),
/*   OC   */("vrmmax-current",                    false, 0x58),
/* Options*/("tctl-temp",                         true,  0x3f),
            ("cHTC-temp",                         false, 0x59),
            ("pbo-scalar",                        false, 0x5b),
            ("get-pbo-scalar",                    false, 0x6d),
            ("oc-clk",                            false, 0x5f),
            ("per-core-oc-clk",                   false, 0x60),
            ("oc-volt",                           false, 0x61),
            ("set-coall",                         false, 0x07),
            ("set-cogfx",                         false, 0xA7),
            ("set-coper",                         false, 0x06),
            ("set-coall",                         true,  0x36),
            ("set-coper",                         true,  0x35),
            ("enable-oc",                         false, 0x5d),
            ("disable-oc",                        false, 0x5e),
            ("set-boost-limit-frequency",         true,  0x2b), // Works very cool!
            ("set-vddoff-vid",                    true,  0x4b),
            ("set-fll-btc-enable",                true,  0x37), // 0 - True, 1 - False
            ("get-sustained-power-and-thm-limit", true,  0x23), 
            ("get-overclocking-support",          false, 0x6f), // From Ryzen Master Raphael
            ("get-max-cpu-clk",                   false, 0x6e),
            ("get-min-gfx-clk",                   false, 0xCe),
            ("get-max-gfx-clk",                   false, 0xCf),
            ("get-curr-gfx-clk",                  false, 0xD8),
            ("disable-prochot",                   false, 0x5d), // Args 0x1000000 to Disable Prochot, Args 0x0 to Enable O
            ("get-coper-options",                 false, 0xD5),
            ("get-cogfx-options",                 false, 0xD7),
            ("get-pbo-fused-vrmsoc-current",      false, 0xD9), // Possibly unavailable, need to check on other motherboards
            ("get-pbo-fused-vrmtdc-limit",        false, 0xDb),
            ("get-pbo-fused-slow-limit",          false, 0xDc), 
            ("get-pbo-fused-apu-slow-limit",      false, 0xDa), 
            ("get-pbo-fused-tctl-temp",           false, 0xDe) 
        ];
    }
}