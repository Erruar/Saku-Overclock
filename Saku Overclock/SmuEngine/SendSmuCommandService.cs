using System.Globalization;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers;
using Saku_Overclock.SmuEngine.SmuMailBoxes;
using Saku_Overclock.Views;
using static Saku_Overclock.Services.CpuService;

namespace Saku_Overclock.SmuEngine;

/*Created by Serzhik Sakurazhima*/

public class SendSmuCommandService : ISendSmuCommandService
{
    // Адреса
    private static uint _rsmuRsp;
    private static uint _rsmuArg;
    private static uint _rsmuCmd;

    private static uint _mp1Rsp;
    private static uint _mp1Arg;
    private static uint _mp1Cmd;

    // Профили и команды
    private readonly IAppSettingsService AppSettings = App.GetService<IAppSettingsService>();
    private readonly IPresetManagerService PresetManager = App.GetService<IPresetManagerService>();
    private readonly ICpuService Cpu = App.GetService<ICpuService>();
    private Smusettings _smuSettings = new();

    // Связанное с железом
    private static List<(string, bool, uint)>? _commands;
    private CodenameGeneration _codenameGeneration = CodenameGeneration.Unknown;

    // Флаги
    private bool _saveInfo;
    private bool _cancelRange;
    private string _checkAdjLine = string.Empty;
    private bool _dangerSettingsApplied;
    private bool? _isOlderGeneration;
    private bool _safeReapply = true;
    private bool _lockCodenameGeneration;

    /// <summary>
    ///  Список строк для проверки на совпадение, если есть - не применять, так как такое переприменение может привести к нестабильности системы
    /// </summary>
    private readonly List<string> _terminateCommands =
        [
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
        ];

    public SendSmuCommandService()
    {
        _safeReapply = AppSettings.ReapplySafeOverclock;
        SetCodeNameGeneration();
    }

    #region JSON

    /// <summary>
    ///  Загрузка параметров быстрых команд Smu
    /// </summary>
    private void SmuSettingsLoad()
    {
        try
        {
            var folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                               @"\SakuOverclock\smusettings.json";
            if (Directory.Exists(folderPath)) 
            {
                _smuSettings = JsonConvert.DeserializeObject<Smusettings>(
                    File.ReadAllText(folderPath)) ??
                               new Smusettings();
            }
            else
            {
                _smuSettings = new Smusettings();
            }
        }
        catch (Exception ex)
        {
            JsonRepair();
            LogHelper.TraceIt_TraceError(ex);
        }
    }

    /// <summary>
    ///  Восстановление настроек JSON
    /// </summary>
    private void JsonRepair()
    {
        _smuSettings = new Smusettings();
        try
        {
            Directory.CreateDirectory(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) +
                @"\SakuOverclock\smusettings.json",
                JsonConvert.SerializeObject(_smuSettings, Formatting.Indented));
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
                JsonConvert.SerializeObject(_smuSettings, Formatting.Indented));
        }
    }

    #endregion

    #region Apply Overclock Options

    #region Quick Smu Commands

    /// <summary>
    ///  Конвертер адреса в Uint
    /// </summary>
    private static bool TryConvertToUint(string text, out uint address)
    {
        return uint.TryParse(
            text.Trim(),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out address
        );
    }

    /// <summary>
    /// Применяет быстрые команды SMU в зависимости от режима запуска
    /// </summary>
    /// <param name="startup">true - применяются команды при запуске (Startup или ApplyWith), false - только ApplyWith</param>
    public void ApplyQuickSmuCommand(bool startup)
    {
        SmuSettingsLoad();

        if (_smuSettings.QuickSmuCommands == null)
        {
            return;
        }

        if (AppSettings.Preset != -1 && PresetManager.Presets[AppSettings.Preset].SmuEnabled == false)
        {
            return;
        }

        for (var i = 0; i < _smuSettings.QuickSmuCommands.Count; i++)
        {
            var command = _smuSettings.QuickSmuCommands[i];

            // Определяем, нужно ли применять команду
            if (command.ApplyWith || (startup && command.Startup))
            {
                ApplySingleSmuCommand(i);
            }
        }
    }

    /// <summary>
    /// Применяет конкретную быструю команду SMU
    /// </summary>
    private void ApplySingleSmuCommand(int commandIndex)
    {
        try
        {
            var quickCommand = _smuSettings.QuickSmuCommands?[commandIndex];
            if (quickCommand == null)
            {
                LogHelper.LogError($"Quick command at index {commandIndex} not found");
                return;
            }

            var mailbox = _smuSettings.MailBoxes?[quickCommand.MailIndex];
            if (mailbox == null)
            {
                LogHelper.LogError($"Mailbox at index {quickCommand.MailIndex} not found");
                return;
            }

            if (!TryConvertToUint(mailbox.Cmd, out var addrMsg) ||
                !TryConvertToUint(mailbox.Rsp, out var addrRsp) ||
                !TryConvertToUint(mailbox.Arg, out var addrArg) ||
                !TryConvertToUint(quickCommand.Command, out var command))
            {
                LogHelper.LogError($"Failed to parse mailbox addresses or command for index {commandIndex}");
                return;
            }

            var quickMailbox = new SmuAddressSet(addrMsg, addrRsp, addrArg);

            var args = MakeCmdArgs();
            var userArgs = quickCommand.Argument.Trim().Split(',');

            for (var i = 0; i < userArgs.Length && i < args.Length; i++)
            {
                if (!TryConvertToUint(userArgs[i], out args[i]))
                {
                    LogHelper.LogError($"Failed to parse argument at position {i}: '{userArgs[i]}'");
                    return;
                }
            }    

            var status = Cpu.SendSmuCommand(quickMailbox, command, ref args);
            if (status != SmuStatus.OK)
            {
                LogHelper.LogError(StatusCommandParser(status));
            }
        }
        catch (Exception ex)
        {
            LogHelper.LogError($"Failed to apply SMU command at index {commandIndex}: {ex.Message}");
        }
    }

    public static uint[] MakeCmdArgs(uint[] args, uint maxArgs = 6u)
    {
        var array = new uint[maxArgs];
        checked
        {
            var num = (uint)Math.Min(maxArgs, args.Length);
            for (var i = 0; i < num; i++)
            {
                array[i] = args[i];
            }

            return array;
        }
    }

    public static uint[] MakeCmdArgs(uint arg = 0u, uint maxArgs = 6u)
    {
        return MakeCmdArgs([arg], maxArgs);
    }

    #endregion

    #region Overclock Options

    /// <summary>
    ///  Применение параметров разгона из строк RyzenADJ в команды Smu 
    /// </summary>
    public async void Translate(string ryzenAdjString, bool save)
    {
        try
        {
            _saveInfo = save;
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
                            && _dangerSettingsApplied /* Если уже были применены опасные настройки */
                            && !save /* Если пользователь сам их не выставляет */
                            && _safeReapply /* Если включено безопасное применение */
                            && _terminateCommands.Any(ryzenAdjCommand.Contains)) // Если есть совпадения в командах
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
                                       .Replace("=", null)
                                       .Replace("--", null);
                            if (command[(ryzenAdjCommand.IndexOf('=') + 1)..]
                                .Contains(',')) // Если это составная команда с не нулевым аргументом
                            {
                                var parts = command[(ryzenAdjCommand.IndexOf('=') + 1)..]
                                    .Split(','); // узнать аргументы, разделив их
                                if (parts.Length == 2 && uint.TryParse(parts[1], out var commaValue))
                                {
                                    await ApplySettings(ryzenAdjCommandString, 0x0, commaValue); // Применить только второй аргумент
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
                                // Значение после знака равно
                                var ryzenAdjCommandValueString = command[(ryzenAdjCommand.IndexOf('=') + 1)..];
                                if (ryzenAdjCommandValueString.Contains('='))
                                {
                                    ryzenAdjCommandValueString = ryzenAdjCommandValueString.Replace("=", null);
                                }

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
                        await LogHelper.TraceIt_TraceError(ex);
                    }
                }

                ПараметрыPage.SettingsApplied = true;
                _dangerSettingsApplied = true;
                _saveInfo = false;
            }
            catch (Exception ex)
            {
                await LogHelper.TraceIt_TraceError(ex);
            }

            _checkAdjLine = ryzenAdjString;
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    /// <summary>
    ///  Находит команду и применяет параметр разгона с помощью ApplyThisWithStatus
    /// </summary>
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

            if (_commands == null)
            {
                SetCodeNameGeneration();
            }

            // Найти код команды по имени
            var matchingCommands = _commands?.Where(c => c.Item1 == commandName);
            var commands = matchingCommands?.ToList();
            if (commands != null && commands.Count != 0)
            {
                // Реализация fallback механизма - пробуем команды по очереди до первого успеха
                var commandAppliedSuccessfully = false;
                SmuStatus? lastStatus = null; // Сохраняем последний статус для отображения

                foreach (var command in commands)
                {
                    try
                    {
                        // Применить команду и получить статус
                        var status = await ApplyThisWithStatus(command.Item2 ? 1 : 0, command.Item3, args, command.Item1);
                        lastStatus = status; // Сохраняем последний статус

                        if (status == SmuStatus.OK)
                        {
                            // Команда применилась успешно - выходим из цикла
                            commandAppliedSuccessfully = true;
                            break;
                        }

                        // Команда не применилась - ждем 20мс перед попыткой следующей
                        await Task.Delay(20);
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
                    if (lastStatus != null && lastStatus != SmuStatus.OK)
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
            await LogHelper.TraceIt_TraceError(ex);
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                       CommandNameParser(commandName) + "\" " +
                                       "Param_SMU_Command_Unavailable".GetLocalized();
        }
    }

    /// <summary>
    ///  Применяет команду Smu
    /// </summary>
    private async Task<SmuStatus?> ApplyThisWithStatus(int mailbox, uint command, uint[] args, string commandName)
    {
        uint addrMsg = 0;
        uint addrRsp = 0;
        uint addrArg = 0;
        try
        {
            SetCodeNameGeneration();

            if (mailbox == 0)
            {
                addrMsg = _rsmuCmd;
                addrRsp = _rsmuRsp;
                addrArg = _rsmuArg;
            }
            else
            {
                addrMsg = _mp1Cmd;
                addrRsp = _mp1Rsp;
                addrArg = _mp1Arg;
            }

            if (!_saveInfo && commandName == "stopcpu-freqto-ramstate")
            {
                return SmuStatus.OK; // Пропускаем команду но возвращаем OK, для безопасности системы
            }
            if (Cpu.IsRaven &&
                (commandName == "min-gfxclk" || commandName == "max-gfxclk"
                || commandName == "min-socclk-frequency" || commandName == "max-socclk-frequency"
                || commandName == "min-fclk-frequency" || commandName == "max-fclk-frequency"
                || commandName == "min-vcn" || commandName == "max-vcn"
                || commandName == "min-lclk" || commandName == "max-lclk"))
            {
                // 0 - SoC-clk
                // 1 - Fclk
                // 2 - Vcn
                // 3 - Lclk
                // 4 - Gfx-clk
                var mode = commandName.Contains("socclk") ? 0 : (
                           commandName.Contains("fclk") ? 1 : (
                           commandName.Contains("vcn") ? 2 : (
                           commandName.Contains("lclk") ? 3 : (
                           commandName.Contains("gfx") ? 4 : 3))));

                RavenSetSubsystemMinMaxFrequency(args, mode, commandName.Contains("max"));

                return SmuStatus.OK; // Команда применена, другим путём
            }

            var newMailbox = new SmuAddressSet(addrMsg, addrRsp, addrArg);

            var status = Cpu.SendSmuCommand(newMailbox, command, ref args);

            return status;
        }
        catch (Exception ex)
        {
            await LogHelper.TraceIt_TraceError("[SendSmuCommand]@SmuCommandsSafeApply - " + ex + $"\nCMD: {command}.{commandName}, ARGS{args[0]}.{args[1]},MSG:{addrMsg}, ARG:{addrArg}, RSP: {addrRsp}");
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + CommandNameParser(commandName) +
                                       "Param_SMU_Command_Error".GetLocalized();
            return null;
        }
    }

    /// <summary>
    ///  Применяет параметры подсистем процессора на системах с процессорами RavenRidge, используя AMDGPU Smu
    /// </summary>
    /// <param name="mode">0 - SoC-clk, 1 - Fclk, 2 - Vcn, 3 - Lclk, 4 - Gfx-clk</param>
    /// <param name="args">command arguments</param>
    /// <param name="setMax">set maximum or minimum limit</param>
    private void RavenSetSubsystemMinMaxFrequency(uint[] args, int mode = 0, bool setMax = false)
    {
        var amdGpuMailbox = new SmuAddressSet(0x3B10A08, 0x3B10A68, 0x3B10A48);
        
        var command = mode switch
        {
            0 => setMax ? 0x32u : 0x21u,
            1 => setMax ? 0x33u : 0x12u,
            2 => setMax ? 0x34u : 0x28u,
            3 => setMax ? 0x14u : 0x23u,
            4 => setMax ? 0x30u : 0x31u,
            _ => setMax ? 0x30u : 0x31u,
        };

        var status = Cpu.SendSmuCommand(amdGpuMailbox, command, ref args);

        if (status != SmuStatus.OK)
        {
            LogHelper.TraceIt_TraceError("[SendSmuCommand+RavenCommands]@Raven_SetGpuMinMaxFrequency_StatusNotOK: - " + status);
        }
    }


    /// <summary>
    ///  Преобразователь статуса отправки команды Smu в текст
    /// </summary>
    private static string StatusCommandParser(SmuStatus? status)
    {
        return status switch
        {
            null => "\"" + "SMUErrorPlatformDesc".GetLocalized() + "\"",
            SmuStatus.CMD_REJECTED_PREREQ => "\"" + "SMUErrorPrereqDesc".GetLocalized() + "\"",
            SmuStatus.CMD_REJECTED_BUSY => "\"" + "SMUErrorBusyDesc".GetLocalized() + "\"",
            SmuStatus.FAILED => "\"" + "SMUErrorFailedDesc".GetLocalized() + "\"",
            SmuStatus.UNKNOWN_CMD => "\"" + "SMUErrorUnknownDesc".GetLocalized() + "\"",
            _ => "\"" + "SMUErrorStatusDesc".GetLocalized() + "\""
        };
    }

    /// <summary>
    ///  Парсер для отображения на каких параметрах возникла ошибка применения
    /// </summary>
    private string CommandNameParser(string commandName)
    {
        var isFixZeroFourGhz = AppSettings.Preset >= 0 &&
           AppSettings.Preset < PresetManager.Presets.Length &&
           PresetManager.Presets[AppSettings.Preset].Gpu16;

        static string L(string name) 
        {
            try
            {
                return name.GetLocalized();
            }
            catch
            {
                return "Unknown";
            }
        }

        return commandName switch
        {
            "enable-feature" => isFixZeroFourGhz ? L("Param_GPU_g16/Text") : L("Param_SMU_Func_Text/Text"),
            "disable-feature" => isFixZeroFourGhz ? L("Param_GPU_g16/Text") : L("Param_SMU_Func_Text/Text"),
            "stapm-limit" => L("Param_CPU_c2/Text"),
            "vrm-current" => L("Param_VRM_v2/Text"),
            "vrmmax-current" => L("Param_VRM_v1/Text"),
            "tctl-temp" => L("Param_CPU_c1/Text"),
            "pbo-scalar" => L("Param_ADV_a15/Text"),
            "oc-clk" => L("Param_ADV_a11/Text"),
            "per-core-oc-clk" => L("Param_ADV_a11/Text"),
            "oc-volt" => L("Param_ADV_a12/Text"),
            "set-coall" => L("Param_CO_O1/Text"),
            "set-cogfx" => L("Param_CO_O2/Text"),
            "set-coper" => L("Param_CCD1_CO_Section/Text"),
            "enable-oc" => L("Param_ADV_a14_E/Content"),
            "disable-oc" => L("Param_ADV_a14_E/Content"),
            "stapm-time" => L("Param_CPU_c5/Text"),
            "fast-limit" => L("Param_CPU_c3/Text"),
            "slow-limit" => L("Param_CPU_c4/Text"),
            "slow-time" => L("Param_CPU_c6/Text"),
            "cHTC-temp" => L("Param_CPU_c7/Text"),
            "apu-skin-temp" => L("Param_ADV_a6/Text"),
            "vrmsoc-current" => L("Param_VRM_v4/Text"),
            "vrmsocmax-current" => L("Param_VRM_v3/Text"),
            "prochot-deassertion-ramp" => L("Param_VRM_v7/Text"),
            "psi3cpu_current" => L("Param_ADV_a4/Text"),
            "psi3gfx_current" => L("Param_ADV_a5/Text"),
            "gfx-clk" => L("Param_ADV_a10/Text"),
            "power-saving" => L("Param_ADV_a13_E/Content"),
            "max-performance" => L("Param_ADV_a13_U/Content"),
            "apu-slow-limit" => L("Param_ADV_a8/Text"),
            "dgpu-skin-temp" => L("Param_ADV_a7/Text"),
            "psi0-current" => L("Param_VRM_v5/Text"),
            "psi0soc-current" => L("Param_VRM_v6/Text"),
            "skin-temp-limit" => L("Param_ADV_a9/Text"),
            "max-cpuclk" => L("Param_GPU_g12/Text"),
            "min-cpuclk" => L("Param_GPU_g11/Text"),
            "max-gfxclk" => L("Param_GPU_g10/Text"),
            "min-gfxclk" => L("Param_GPU_g9/Text"),
            "max-socclk-frequency" => L("Param_GPU_g2/Text"),
            "min-socclk-frequency" => L("Param_GPU_g1/Text"),
            "max-fclk-frequency" => L("Param_GPU_g4/Text"),
            "min-fclk-frequency" => L("Param_GPU_g3/Text"),
            "max-vcn" => L("Param_GPU_g6/Text"),
            "min-vcn" => L("Param_GPU_g5/Text"),
            "max-lclk" => L("Param_GPU_g8/Text"),
            "min-lclk" => L("Param_GPU_g7/Text"),
            "setcpu-freqto-ramstate" => L("Param_GPU_g16/Text"),
            "stopcpu-freqto-ramstate" => L("Param_GPU_g16/Text"),
            "set-gpuclockoverdrive-byvid" => L("Param_ADV_a10/Text"),
            _ => "Unknown"
        };
    }

    #endregion

    #region Send Smu command range

    /// <summary>
    ///  Применение диапазона аргументов для команды Smu (для отладки)
    /// </summary>
    public async void SendRange(string commandIndex, string startIndex, string endIndex, int mailbox, bool log)
    {
        try
        {
            _cancelRange = false;
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
                        if (_cancelRange)
                        {
                            _cancelRange = false;
                            sw.WriteLine(@"//------CANCEL------\\");
                            RangeCompleted?.Invoke(this, EventArgs.Empty);
                            return;
                        }

                        var args = MakeCmdArgs();
                        TryConvertToUint(_smuSettings?.MailBoxes![mailbox].Cmd!, out var addrMsg);
                        TryConvertToUint(_smuSettings?.MailBoxes![mailbox].Rsp!, out var addrRsp);
                        TryConvertToUint(_smuSettings?.MailBoxes![mailbox].Arg!, out var addrArg);
                        TryConvertToUint(commandIndex, out var command);

                        var newMailbox = new SmuAddressSet(addrMsg, addrRsp, addrArg);
                        
                        args[0] = j;
                        try
                        {
                            var status = Cpu.SendSmuCommand(newMailbox, command, ref args);
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
                await LogHelper.TraceIt_TraceError(ex);
            }
        }
        catch (Exception e)
        {
            await LogHelper.TraceIt_TraceError(e);
        }
    }

    /// <summary>
    ///  Отмена применения диапазона аргументов для команды Smu (для отладки)
    /// </summary>
    public void CancelRange() => _cancelRange = true;

    /// <summary>
    ///  Применение диапазона аргументов для команды Smu завершено (для отладки)
    /// </summary>
    public event EventHandler? RangeCompleted;

    #endregion

    #endregion

    #region Generation helpers

    /// <summary>
    ///  Вспомогательный метод для определения старых поколений, для применения команд Smu без задержек
    /// </summary>
    private bool IsOlderGeneration()
    {
        _isOlderGeneration ??= _codenameGeneration is CodenameGeneration.FP4
            or CodenameGeneration.AM4_V1 or CodenameGeneration.AM4_V2
            or CodenameGeneration.FP5 or CodenameGeneration.FP6;

        return _isOlderGeneration == true; // Использовать кешированное значение вместо постоянного прогона функций
    }

    /// <summary>
    ///  Глобальный метод для установки адресов и команд Smu для каждого сокета и кодового имени процессоров
    /// </summary>
    private void SetCodeNameGeneration()
    {
        _codenameGeneration = Cpu.GetCodenameGeneration();
        switch (_codenameGeneration)
        {
            case CodenameGeneration.FP4: 
                Socket_FP4(); 
                break;
            case CodenameGeneration.FP5: 
                Socket_FP5(); 
                break;
            case CodenameGeneration.FP6: 
                Socket_FP6(); 
                break;
            case CodenameGeneration.FF3: 
                Socket_FF3(); 
                break;
            case CodenameGeneration.FP7: 
                Socket_FT6_FP7_FP8_FP11(false); 
                break;
            case CodenameGeneration.FP8: 
                Socket_FT6_FP7_FP8_FP11(true); 
                break;
            case CodenameGeneration.AM4_V1: 
                Socket_AM4_V1(); 
                break;
            case CodenameGeneration.AM4_V2: 
                Socket_AM4_V2(); 
                break;
            case CodenameGeneration.AM5: 
                Socket_AM5_V1(); 
                break;
            default: 
                GetCodeNameGeneration(); 
                break;
        }
    }

    /// <summary>
    ///  Возвращает имя платформы, CodeNameGeneration
    /// </summary>
    public CodenameGeneration GetCodeNameGeneration()
    {
        if (_codenameGeneration == CodenameGeneration.Unknown && !_lockCodenameGeneration)
        {
            _lockCodenameGeneration = true;
            SetCodeNameGeneration();
        }
        return _codenameGeneration;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///  Возвращает значение безопасного применения команд или назначает его
    /// </summary>
    public bool GetSetSafeReapply(bool? value = null)
    {
        if (value != null)
        {
            _safeReapply = value == true;
        }
        return _safeReapply;
    }

    /// <summary>
    ///  Возвращает команду Cpu Per Core Curve Optimizer при её наличии
    /// </summary>
    public uint ReturnCoPer(bool isMp1 = true)
    {
        var matchingCommands = _commands?.Where(c => c.Item1 == "set-coper" && c.Item2 == isMp1);
        var commands = matchingCommands!.ToList();
        if (commands.Count != 0)
        {
            return commands.Select(command => command.Item3).FirstOrDefault();
        }

        return 0U;
    }

    /// <summary>
    ///  Возвращает значение установленное TDP 
    /// </summary>
    public double ReturnCpuPowerLimit()
    {
        var actualCommand = 0x0U;
        var matchingCommandsMp1 = _commands?.Where(c => c is { Item1: "get-sustained-power-and-thm-limit", Item2: true });
        var matchingCommandsRsmu = _commands?.Where(c => c is { Item1: "get-sustained-power-and-thm-limit", Item2: false });
        var commands = matchingCommandsMp1?.ToList();
        var commands1 = matchingCommandsRsmu?.ToList();
        var mailbox = 1;
        if (commands != null && commands.Count != 0)
        {
            actualCommand = commands.Select(command => command.Item3).FirstOrDefault();
            mailbox = 1; // MP1
        }
        if (commands1 != null && commands1.Count != 0)
        {
            actualCommand = commands1.Select(command => command.Item3).FirstOrDefault();
            mailbox = 0; // RSMU
        }

        if ((commands?.Count != 0 || commands1?.Count != 0) && actualCommand != 0x0U)
        {
            uint addrMsg;
            uint addrRsp;
            uint addrArg;

            if (mailbox == 0)
            {
                addrMsg = _rsmuCmd;
                addrRsp = _rsmuRsp;
                addrArg = _rsmuArg;
            }
            else
            {
                addrMsg = _mp1Cmd;
                addrRsp = _mp1Rsp;
                addrArg = _mp1Arg;
            }

            var newMailbox = new SmuAddressSet(addrMsg, addrRsp, addrArg);

            var args = MakeCmdArgs();
            var status = Cpu.SendSmuCommand(newMailbox, actualCommand, ref args);

            if (status != SmuStatus.OK)
            {
                LogHelper.TraceIt_TraceError("OcFinderCpuPowerIsNotDetected".GetLocalized());
                LogHelper.LogError(status.ToString());
            }

            if (args[0] != 0x0)
            {
                return (args[0] & 0x00FF0000) >> 16;
            }
        }

        return -1;
    }

    /// <summary>
    ///  Проверка доступности андервольтинга
    /// </summary>
    public bool ReturnUndervoltingAvailability()
    {
        var actualCommand = 0x0U;
        var matchingCommandsMp1 = _commands?.Where(c => c is { Item1: "get-coper-options", Item2: true });
        var matchingCommandsRsmu = _commands?.Where(c => c is { Item1: "get-coper-options", Item2: false });
        var commands = matchingCommandsMp1?.ToList();
        var commands1 = matchingCommandsRsmu?.ToList();
        var mailbox = 1;
        if (commands != null && commands.Count != 0)
        {
            actualCommand = commands.Select(command => command.Item3).FirstOrDefault();
            mailbox = 1; // MP1
        }
        if (commands1 != null && commands1.Count != 0)
        {
            actualCommand = commands1.Select(command => command.Item3).FirstOrDefault();
            mailbox = 0; // RSMU
        }

        if ((commands?.Count != 0 || commands1?.Count != 0) && actualCommand != 0x0U)
        {
            uint addrMsg;
            uint addrRsp;
            uint addrArg;

            if (mailbox == 0)
            {
                addrMsg = _rsmuCmd;
                addrRsp = _rsmuRsp;
                addrArg = _rsmuArg;
            }
            else
            {
                addrMsg = _mp1Cmd;
                addrRsp = _mp1Rsp;
                addrArg = _mp1Arg;
            }

            var newMailbox = new SmuAddressSet(addrMsg, addrRsp, addrArg);

            var args = MakeCmdArgs();
            var status = Cpu.SendSmuCommand(newMailbox, actualCommand, ref args);

            if (status != SmuStatus.OK)
            {
                LogHelper.LogError("OcFinderUnableToGetUndervoltingStatus".GetLocalized() + status);
            }

            if (args[0] != 0x0)
            {
                return true;
            }

            return TrySetUndervolt();
        }

        if (_codenameGeneration == CodenameGeneration.FP5)
        {
            return true;
        }

        return TrySetUndervolt();
    }

    /// <summary>
    ///  Пытается установить андервольтинг (для проверки на его доступность)
    /// </summary>
    private bool TrySetUndervolt()
    {
        var actualCommand = 0x0U;
        var matchingCommandsMp1 = _commands?.Where(c => c is { Item1: "set-coall", Item2: true });
        var matchingCommandsRsmu = _commands?.Where(c => c is { Item1: "set-coall", Item2: false });
        var commands = matchingCommandsMp1?.ToList();
        var commands1 = matchingCommandsRsmu?.ToList();
        var mailbox = 1;
        if (commands != null && commands.Count != 0)
        {
            actualCommand = commands.Select(command => command.Item3).FirstOrDefault();
            mailbox = 1; // MP1
        }
        if (commands1 != null && commands1.Count != 0)
        {
            actualCommand = commands1.Select(command => command.Item3).FirstOrDefault();
            mailbox = 0; // RSMU
        }

        if ((commands?.Count != 0 || commands1?.Count != 0) && actualCommand != 0x0U)
        {
            uint addrMsg;
            uint addrRsp;
            uint addrArg;

            if (mailbox == 0)
            {
                addrMsg = _rsmuCmd;
                addrRsp = _rsmuRsp;
                addrArg = _rsmuArg;
            }
            else
            {
                addrMsg = _mp1Cmd;
                addrRsp = _mp1Rsp;
                addrArg = _mp1Arg;
            }

            var newMailbox = new SmuAddressSet(addrMsg, addrRsp, addrArg);

            var args = MakeCmdArgs();
            var status = Cpu.SendSmuCommand(newMailbox, actualCommand, ref args);

            if (status != SmuStatus.OK)
            {
                LogHelper.TraceIt_TraceError("OcFinderUnableToSetUndervoltingStatus".GetLocalized());
                LogHelper.LogError(status.ToString());
                return false;
            }

            return true;
        }

        return false;
    }

    #endregion

    #region Smu Command set

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
        _mp1Cmd = 0x13000000;
        _mp1Rsp = 0x13000010;
        _mp1Arg = 0x13000020;

        _commands =
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
        _mp1Cmd = 0x3B10528;
        _mp1Rsp = 0x3B10564;
        _mp1Arg = 0x3B10998;

        _rsmuCmd = 0x3B10A20;
        _rsmuRsp = 0x3B10A80;
        _rsmuArg = 0x3B10A88;

        _commands =
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
        _mp1Cmd = 0x3B10528;
        _mp1Rsp = 0x3B10564;
        _mp1Arg = 0x3B10998;

        _rsmuCmd = 0x3B10A20;
        _rsmuRsp = 0x3B10A80;
        _rsmuArg = 0x3B10A88;

        _commands =
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
        _mp1Cmd = 0x3B10528;
        _mp1Rsp = 0x3B10578;
        _mp1Arg = 0x3B10998;

        _rsmuCmd = 0x3B10a20;
        _rsmuRsp = 0x3B10a80;
        _rsmuArg = 0x3B10a88;

        _commands =
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

    private static void Socket_FT6_FP7_FP8_FP11(bool isStrix)
    {
        if (isStrix)
        {
            _mp1Cmd = 0x3B10928;
            _mp1Rsp = 0x3B10978;
            _mp1Arg = 0x3B10998;
        }
        else
        {
            _mp1Cmd = 0x3B10528;
            _mp1Rsp = 0x3B10578;
            _mp1Arg = 0x3B10998;
        }

        _rsmuCmd = 0x3B10a20;
        _rsmuRsp = 0x3B10a80;
        _rsmuArg = 0x3B10a88;

        _commands =
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
        _mp1Cmd = 0X3B10528;
        _mp1Rsp = 0X3B10564;
        _mp1Arg = 0X3B10598;

        _rsmuCmd = 0x3B1051C;
        _rsmuRsp = 0X3B10568;
        _rsmuArg = 0X3B10590;

        _commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: Ryzen 5 1600
            // SMU command map for early AM4 socket (last update: 2025-11-23)
            ("enable-feature",                    true,  0x09),
            ("fast-limit",                        false, 0x64), // PPT limit
            ("fast-limit",                        true,  0x31), // PPT limit
            ("vrm-current",                       false, 0x65),
            ("vrmmax-current",                    false, 0x66),
            ("tctl-temp",                         false, 0x68), 
/*   OC   */("pbo-scalar",                        false, 0x6a),
/* Options*/("oc-clk",                            false, 0x6c),
            ("oc-clk",                            true,  0x39),
            ("per-core-oc-clk",                   false, 0x6d),
            ("oc-volt",                           false, 0x6e),
            ("oc-volt",                           true,  0x38),
            ("enable-oc",                         true,  0x23),
            ("enable-oc",                         false, 0x6b),
            ("disable-oc",                        true,  0x24),
            ("get-sustained-power-and-thm-limit", true,  0x36),
            ("setcpu-freqto-ramstate",            true,  0x23),
/*  AcBtc */("stopcpu-freqto-ramstate",           true,  0x24),
            ("stopcpu-freqto-ramstate",           true,  0x25),
        ];
    }

    private static void Socket_AM4_V2()
    {
        _mp1Cmd = 0x3B10530;
        _mp1Rsp = 0x3B1057C;
        _mp1Arg = 0x3B109C4;

        _rsmuCmd = 0x3B10524;
        _rsmuRsp = 0x3B10570;
        _rsmuArg = 0x3B10A40;

        _commands =
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
        _mp1Cmd = 0x3B10530;
        _mp1Rsp = 0x3B1057C;
        _mp1Arg = 0x3B109C4;

        _rsmuCmd = 0x3B10524;
        _rsmuRsp = 0x3B10570;
        _rsmuArg = 0x3B10A40;

        _commands =
        [
            // Store the commands
            // Those commands are 100% tested
            // Tested on: Ryzen 9 7900X3D
            // SMU command map for AM5 socket (last update: 2025-07-05)
            ("enable-feature",                    true,  0x03),
            ("disable-feature",                   true,  0x04), // Can be locked
            ("stapm-limit",                       true,  0x4f), // Set CPU Stapm value! Not affect on PPT. Works only if Stapm feature is enabled on platform BE CAREFUL! If you apply this command and then "fast-limit" - actual PPT limit will NOT be saved thats why I commented that command for safety
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
            ("set-cogfx",                         false, 0xA7), // 0xA5 0x04B00190 ??? // 0x70 0x16DA PBO Max freq override
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

    #endregion

}