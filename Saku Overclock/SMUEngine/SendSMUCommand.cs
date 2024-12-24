using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.JsonContainers; 
using Saku_Overclock.Views;
using ZenStates.Core;

namespace Saku_Overclock.SMUEngine;

/*Created by Serzhik Sakurazhima*/
/*Этот класс является частью программного обеспечения для управления разгоном процессоров.Он содержит методы и данные для отправки команд SMU (System Management Unit) на процессор.Вот краткое описание содержимого класса:

Приватные статические поля:

RSMU_RSP, RSMU_ARG, RSMU_CMD: содержат адреса для отправки команд SMU на системы AMD.
MP1_RSP, MP1_ARG, MP1_CMD: содержат адреса для отправки команд SMU на системы с другой архитектурой.
Публичные свойства и поля:

commands: список кортежей, хранящих информацию о командах (название, флаг применения, адрес).
cancelrange: флаг для отмены диапазона команд.
Методы:

OC_Detect: пытается определить, поддерживает ли процессор возможность разгона.
Init_OC_Mode: запускает процесс установки режима разгона.
SmuSettingsSave, SmuSettingsLoad, ProfileLoad, SettingsServiceLoad, SettingsServiceSave: методы для сохранения и загрузки настроек из JSON-файлов.
Play_Invernate_QuickSMU, ApplySettings, ApplyThis, SendRange: устаревшие методы, отвечающие за отправку команд SMU на процессор.
JsonRepair: метод для восстановления JSON-файлов в случае их повреждения.
CancelRange: устанавливает флаг отмены диапазона команд.
Приватные методы для определения адресов и отправки команд SMU на процессор.
Этот класс является частью большой системы, управляющей разгоном процессоров, и содержит функционал для загрузки/сохранения настроек, определения возможности разгона на данной архитектуре и отправки соответствующих команд на процессор.*/
internal class SendSmuCommand
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
    public static Cpu.CodeName Codename;
    private static readonly IAppSettingsService SettingsService = App.GetService<IAppSettingsService>();
    private static string _cpuCodenameString = string.Empty;
    private Smusettings _smusettings = new(); 
    private static readonly IAppNotificationService NotificationsService = App.GetService<IAppNotificationService>(); 
    private readonly Mailbox _testMailbox = new();
    private Profile[] _profile = new Profile[1];
    private bool _cancelrange;
    private bool _dangersettingsapplied;
    private string _checkAdjLine = "nothing to show. Hehe";

    public static bool SafeReapply
    {
        get;
        set;
    } = true;

    public SendSmuCommand()
    {
        try
        {
            _cpu ??= CpuSingleton.GetInstance();
            Codename = _cpu.info.codeName;
        }
        catch (Exception ex)
        {
            App.GetService<IAppNotificationService>()
                .Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
            TraceIt_TraceError(ex.ToString());
        }

        try
        {
            SafeReapply = SettingsService.ReapplySafeOverclock;
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
        }
    }

    // JSON
    private void SmuSettingsLoad()
    {
        try
        {
            _smusettings = JsonConvert.DeserializeObject<Smusettings>(File.ReadAllText(
                Environment.GetFolderPath(Environment.SpecialFolder.Personal) + @"\SakuOverclock\smusettings.json")) ?? new Smusettings();
        }
        catch (Exception ex)
        {
            JsonRepair('s');
            TraceIt_TraceError(ex.ToString());
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
            TraceIt_TraceError(ex.ToString());
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
                    App.MainWindow.Close();
                }

                break;
            }
            case 'p':

                _profile = [];
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
                    App.MainWindow.Close();
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

        if (SettingsService.Preset != -1)
        {
            if (_profile[SettingsService.Preset].smuEnabled == false)
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
            TraceIt_TraceError(ex.ToString());
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
                "psi0-current", //Безопасно
                "psi0soc-current",
                "prochot-deassertion-ramp",
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
                "start-gpu-link", //Средне
                "stop-gpu-link",
                "setcpu-freqto-ramstate", //ОПАСНО ПЕРЕПРИМЕНЯТЬ
                "stopcpu-freqto-ramstate",
                "vrmgfx-current", //Безопасно
                "vrmcvip-current",
                "vrmgfxmax_current",
                "psi3cpu_current",
                "psi3gfx_current",
                "gfx-clk",
                "oc-clk",
                "oc-volt",
                "max-performance", //ОПАСНО ПЕРЕПРИМЕНЯТЬ
                "power-saving",
                "disable-oc", //Средне
                "enable-oc",
                "pbo-scalar",
                "set-coall", //ОПАСНО ПЕРЕПРИМЕНЯТЬ
                "set-coper",
                "enable-feature", //Средне
                "disable-feature"
            };

            _saveinfo = save;
            try
            {
                _cpu ??= CpuSingleton.GetInstance();
                if (_cpu != null && Codename != _cpu.info.codeName ||
                    !_cpuCodenameString.Contains(_cpu!.info.codeName.ToString()))
                {
                    Codename = _cpu.info.codeName;
                    _cpuCodenameString = Codename.ToString();
                    if (_cpuCodenameString == string.Empty)
                    {
                        _cpuCodenameString = RyzenAdjWrapper.GetCpuCodename();
                    }
                }

                if (_cpu?.info.codeName == Cpu.CodeName.SummitRidge || Codename == Cpu.CodeName.SummitRidge ||
                    _cpu?.info.codeName == Cpu.CodeName.PinnacleRidge)
                {
                    Socket_AM4_V1();
                }
                else if (_cpu?.info.codeName == Cpu.CodeName.RavenRidge || _cpuCodenameString.Contains("RAVEN") ||
                         _cpu?.info.codeName == Cpu.CodeName.Picasso || _cpuCodenameString.Contains("PICASSO") ||
                         _cpu?.info.codeName == Cpu.CodeName.Dali || _cpuCodenameString.Contains("DALI") ||
                         /*cpu.info.codeName == Cpu.CodeName.Pollock || */
                         _cpu?.info.codeName == Cpu.CodeName.FireFlight)
                {
                    Socket_FT5_FP5_AM4();
                }
                else if (_cpu?.info.codeName is Cpu.CodeName.Matisse or Cpu.CodeName.Vermeer)
                {
                    Socket_AM4_V2();
                }
                else if (_cpu?.info.codeName == Cpu.CodeName.Renoir || _cpuCodenameString.Contains("RENOIR") ||
                         _cpu?.info.codeName == Cpu.CodeName.Lucienne || _cpuCodenameString.Contains("LUCIENNE") ||
                         _cpuCodenameString.Contains("CEZANNE") ||
                         _cpu?.info.codeName == Cpu.CodeName.Cezanne)
                {
                    Socket_FP6_AM4();
                }
                else if (_cpu?.info.codeName == Cpu.CodeName.VanGogh || _cpuCodenameString.Contains("VANGOGH"))
                {
                    Socket_FF3();
                }
                else if (_cpu?.info.codeName == Cpu.CodeName.Mendocino || _cpuCodenameString.Contains("MENDOCINO") ||
                         _cpu?.info.codeName == Cpu.CodeName.Rembrandt || _cpuCodenameString.Contains("REMBRANDT") ||
                         _cpu?.info.codeName == Cpu.CodeName.Phoenix || _cpuCodenameString.Contains("PHOENIX") ||
                         _cpu?.info.codeName == Cpu.CodeName.Phoenix2 || _cpuCodenameString.Contains("HAWKPOINT") ||
                         _cpu?.info.codeName == Cpu.CodeName.StrixPoint || _cpuCodenameString.Contains("STRIXPOINT") ||
                         _cpu?.info.codeName == Cpu.CodeName.DragonRange)
                {
                    Socket_FT6_FP7_FP8();
                }
                else if (_cpu?.info.codeName == Cpu.CodeName.Raphael ||
                 _cpu?.info.codeName == Cpu.CodeName.GraniteRidge || 
                 _cpu?.info.codeName == Cpu.CodeName.Genoa || 
                 _cpu?.info.codeName == Cpu.CodeName.StormPeak || 
                 _cpu?.info.codeName == Cpu.CodeName.Bergamo)
                {
                    Socket_AM5_V1();
                } //Не всё поддерживается, но это будет в будущем исправлено
                else
                {
                    Mp1Cmd = 0x3B10528;
                    Mp1Rsp = 0x3B10564;
                    Mp1Arg = 0x3B10998;
                    RsmuCmd = 0x3B10A20;
                    RsmuRsp = 0x3B10A80;
                    RsmuArg = 0x3B10A88;
                }

                //Убрать последний знак в строке аргументов для применения
                ryzenAdjString = ryzenAdjString.TrimEnd();
                //Разделить команды в Array
                var ryzenAdjCommands = ryzenAdjString.Split(' ');
                ryzenAdjCommands = ryzenAdjCommands.Distinct().ToArray();
                //Выполнить через Array
                foreach (var ryzenAdjCommand in ryzenAdjCommands)
                {
                    await Task.Run(() =>
                    {
                        try
                        {
                            //Проверить есть ли совпадения с листом опасных команд, которые нежелательно переприменять, чтобы не получить краш системы из-за перегрузки SMU
                            if (_checkAdjLine ==
                                ryzenAdjString /*Если прошлое применение совпадает с текущим применением*/
                                && _dangersettingsapplied /*Если уже были применены опасные настройки*/
                                && !save /*Если пользователь сам их не выставляет*/
                                && SafeReapply /*Если включено безопасное применение*/
                                && terminateCommands.Any(tc =>
                                    ryzenAdjCommand.Contains(tc))) //Если есть совпадения в командах
                            {
                                //Ничего не делать 
                                //TraceIt_TraceError("Вы хотите применить опасные параметры?");
                            }
                            else
                            {
                                //TraceIt_TraceError("Применены безопасные параметры?\n" + _ryzenAdjString);
                                var command = ryzenAdjCommand;
                                if (!command.Contains('='))
                                {
                                    command = ryzenAdjCommand + "=0";
                                }

                                //Выяснить какая команда стоит до знака равно
                                var ryzenAdjCommandString =
                                    command.Split('=')[0].Replace("=", null).Replace("--", null);
                                if (command[(ryzenAdjCommand.IndexOf('=') + 1)..]
                                    .Contains(',')) //Если это составная команда с не нулевым аргументом
                                {
                                    var parts = command[(ryzenAdjCommand.IndexOf('=') + 1)..]
                                        .Split(','); //узнать первый аргумент, разделив аргументы
                                    if (parts.Length == 2 && uint.TryParse(parts[1], out var commaValue))
                                    {
                                        ApplySettings(ryzenAdjCommandString, 0x0,
                                            commaValue); //Применить преимущественно второй аргумент
                                    }
                                }
                                else
                                {
                                    //А теперь узнаем что стоит после знака равно
                                    var ryzenAdjCommandValueString = command[(ryzenAdjCommand.IndexOf('=') + 1)..];
                                    //Конвертировать в Uint
                                    if (ryzenAdjCommandValueString.Contains('='))
                                    {
                                        ryzenAdjCommandValueString = ryzenAdjCommandValueString.Replace("=", null);
                                    }

                                    var ryzenAdjCommandValue = Convert.ToUInt32(ryzenAdjCommandValueString);
                                    if (ryzenAdjCommandValue <= 0 && !ryzenAdjCommandString.Contains("coall") &&
                                        !ryzenAdjCommandString.Contains("coper") &&
                                        !ryzenAdjCommandString.Contains("cogfx"))
                                    {
                                        ApplySettings(ryzenAdjCommandString,
                                            0x0); //Если пользователь редактировал конфиги вручную, чтобы программа не крашнулась из-за непредвиденного отрицательного значения
                                    }
                                    else
                                    {
                                        ApplySettings(ryzenAdjCommandString, ryzenAdjCommandValue);
                                    }
                                }
                            }

                            Task.Delay(50); //Задержка перед применением следующей группы команд
                        }
                        catch (Exception ex)
                        {
                            TraceIt_TraceError(ex.ToString());
                        }
                    });
                }

                _dangersettingsapplied = true;
                _saveinfo = false;
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }

            _checkAdjLine = ryzenAdjString;
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    private void ApplySettings(string commandName, uint value, uint value1 = 0)
    {
        try
        {
            var args = new uint[6];
            args[0] = value;
            if (value1 != 0)
            {
                args[1] = value1; //Если команда составная
            }

            //Найти код команды по имени
            var matchingCommands = Commands?.Where(c => c.Item1 == commandName);
            var commands = matchingCommands!.ToList();
            if (commands.Count != 0)
            {
                var tasks = commands.Select(command => Task.Run(() =>
                    {
                        //Применить уже эту команду наконец-то!
                        ApplyThis(command.Item2 ? 1 : 0, command.Item3, args, command.Item1);
                    }))
                    .ToList();

                Task.WaitAll([.. tasks]);
            }
            else
            {
                if (!$"\nCommand '{commandName}' not found".Contains("Command '' not found"))
                {
                    ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                               CommandNameParser(commandName) + "\" " +
                                               "Param_SMU_Command_Unavailable".GetLocalized();
                }
            }
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                       CommandNameParser(commandName) + "\" " +
                                       "Param_SMU_Command_Unavailable".GetLocalized();
        }
    }

    private void ApplyThis(int mailbox, uint command, uint[] args, string commandName)
    {
        try
        {
            _cpu ??= CpuSingleton.GetInstance();
            if (_cpu != null && Codename != _cpu.info.codeName ||
                !_cpuCodenameString.Contains(_cpu!.info.codeName.ToString()))
            {
                Codename = _cpu.info.codeName;
                _cpuCodenameString = Codename.ToString();
                if (_cpuCodenameString == string.Empty)
                {
                    _cpuCodenameString = RyzenAdjWrapper.GetCpuCodename();
                }
            }

            if (_cpu?.info.codeName == Cpu.CodeName.SummitRidge || Codename == Cpu.CodeName.SummitRidge ||
                _cpu?.info.codeName == Cpu.CodeName.PinnacleRidge)
            {
                Socket_AM4_V1();
            }
            else if (_cpu?.info.codeName == Cpu.CodeName.RavenRidge || _cpuCodenameString.Contains("RAVEN") ||
                     _cpu?.info.codeName == Cpu.CodeName.Picasso || _cpuCodenameString.Contains("PICASSO") ||
                     _cpu?.info.codeName == Cpu.CodeName.Dali || _cpuCodenameString.Contains("DALI") ||
                     /*cpu.info.codeName == Cpu.CodeName.Pollock || */
                     _cpu?.info.codeName == Cpu.CodeName.FireFlight)
            {
                Socket_FT5_FP5_AM4();
            }
            else if (_cpu?.info.codeName == Cpu.CodeName.Matisse ||
                     _cpu?.info.codeName == Cpu.CodeName.Vermeer)
            {
                Socket_AM4_V2();
            }
            else if (_cpu?.info.codeName == Cpu.CodeName.Renoir || _cpuCodenameString.Contains("RENOIR") ||
                     _cpu?.info.codeName == Cpu.CodeName.Lucienne || _cpuCodenameString.Contains("LUCIENNE") ||
                     _cpuCodenameString.Contains("CEZANNE") ||
                     _cpu?.info.codeName == Cpu.CodeName.Cezanne)
            {
                Socket_FP6_AM4();
            }
            else if (_cpu?.info.codeName == Cpu.CodeName.VanGogh || _cpuCodenameString.Contains("VANGOGH"))
            {
                Socket_FF3();
            }
            else if (_cpu?.info.codeName == Cpu.CodeName.Mendocino || _cpuCodenameString.Contains("MENDOCINO") ||
                     _cpu?.info.codeName == Cpu.CodeName.Rembrandt || _cpuCodenameString.Contains("REMBRANDT") ||
                     _cpu?.info.codeName == Cpu.CodeName.Phoenix || _cpuCodenameString.Contains("PHOENIX") ||
                     _cpu?.info.codeName == Cpu.CodeName.Phoenix2 || _cpuCodenameString.Contains("HAWKPOINT") ||
                     _cpu?.info.codeName == Cpu.CodeName.StrixPoint || _cpuCodenameString.Contains("STRIXPOINT") ||
                     _cpu?.info.codeName == Cpu.CodeName.DragonRange)
            {
                Socket_FT6_FP7_FP8();
            }
            else if (_cpu?.info.codeName == Cpu.CodeName.Raphael ||
                 _cpu?.info.codeName == Cpu.CodeName.GraniteRidge ||
                 _cpu?.info.codeName == Cpu.CodeName.Genoa ||
                 _cpu?.info.codeName == Cpu.CodeName.StormPeak ||
                 _cpu?.info.codeName == Cpu.CodeName.Bergamo)
            {
                Socket_AM5_V1();
            } //Не всё поддерживается, но это будет в будущем исправлено
            else
            {
                Mp1Cmd = 0x3B10528;
                Mp1Rsp = 0x3B10564;
                Mp1Arg = 0x3B10998;
                RsmuCmd = 0x3B10A20;
                RsmuRsp = 0x3B10A80;
                RsmuArg = 0x3B10A88;
            }

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

            _testMailbox.SMU_ADDR_MSG = addrMsg;
            _testMailbox.SMU_ADDR_RSP = addrRsp;
            _testMailbox.SMU_ADDR_ARG = addrArg;
            if (!_saveinfo && commandName == "stopcpu-freqto-ramstate")
            {
                return;
            } // Чтобы уж точно не осталось в RyzenADJline, так как может крашнуть систему

            var status = _cpu?.smu.SendSmuCommand(_testMailbox, command, ref args);
            if (status != SMU.Status.OK)
            {
                ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + "\"" +
                                           CommandNameParser(commandName) + "\" " +
                                           "Param_SMU_Command_Status".GetLocalized() + " " +
                                           StatusCommandParser(status);
            }
            // Если при применении что-то пошло не так - сказать об ошибке
        }
        catch
        {
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + CommandNameParser(commandName) +
                                       "Param_SMU_Command_Error".GetLocalized();
        }
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

    private static string CommandNameParser(string commandName)
    {
        return commandName switch
        {
            "enable-feature" => "Param_SMU_Func_Text/Text".GetLocalized(),
            "disable-feature" => "Param_SMU_Func_Text/Text".GetLocalized(),
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
            "vrmcvip-current" => "Param_ADV_a2/Text".GetLocalized(),
            "vrmgfx-current" => "Param_ADV_a1/Text".GetLocalized(),
            "vrmgfxmax-current" => "Param_ADV_a3/Text".GetLocalized(),
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
            "oc-volt-variable" => "Param_VRM_v8/Text".GetLocalized(),
            "update-skintemp-error" => "Param_VRM_v8/Text".GetLocalized(),
            "setgpu-arerture-low" => "Param_GPU_g13/Text".GetLocalized(),
            "setgpu-arerture-high" => "Param_GPU_g14/Text".GetLocalized(),
            "start-gpu-link" => "Param_GPU_g15/Text".GetLocalized(),
            "stop-gpu-link" => "Param_GPU_g15/Text".GetLocalized(),
            "setcpu-freqto-ramstate" => "Param_GPU_g16/Text".GetLocalized(),
            "stopcpu-freqto-ramstate" => "Param_GPU_g16/Text".GetLocalized(),
            "set-ulv-vid" => "Param_VRM_v8/Text".GetLocalized(),
            "set-vddoff-vid" => "Param_VRM_v8/Text".GetLocalized(),
            "set-vmin-freq" => "Param_VRM_v8/Text".GetLocalized(),
            "set-gpuclockoverdrive-byvid" => "Param_VRM_v8/Text".GetLocalized(),
            "set-powergate-xgbe" => "Param_VRM_v8/Text".GetLocalized(),
            "enable-cc6filter" => "Param_VRM_v8/Text".GetLocalized(),
            _ => "1"
        };
    }

    public static void TraceIt_TraceError(string error) //Система TraceIt! позволит логгировать все ошибки
    {
        if (error != string.Empty)
        { 
            NotificationsService.Notifies ??= [];
            NotificationsService.Notifies.Add(new Notify
                { Title = "TraceIt_Error".GetLocalized(), Msg = error, Type = InfoBarSeverity.Error });
            NotificationsService.SaveNotificationsSettings();
        }
    }

    public void CancelRange()
    {
        _cancelrange = true;
    }

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

                    // SettingsServiceLoad(); SettingsService.RangeApplied = true; SettingsServiceSave(); 
                    if (log)
                    {
                        sw.WriteLine(@"//------OK------\\");
                    }
                });
            }
            catch (Exception ex)
            {
                TraceIt_TraceError(ex.ToString());
            }
        }
        catch (Exception e)
        {
            TraceIt_TraceError(e.ToString());
        }
    }

    public static uint ReturnCoGfx(Cpu.CodeName codeName)
    {
        Codename = codeName; //если класс неинициализирован - задать правильный Codename
        switch (Codename)
        {
            case Cpu.CodeName.SummitRidge:
            case Cpu.CodeName.PinnacleRidge:
                Socket_AM4_V1();
                break;
            case Cpu.CodeName.RavenRidge:
            case Cpu.CodeName.Picasso:
            case Cpu.CodeName.Dali:
            case Cpu.CodeName.FireFlight:
                Socket_FT5_FP5_AM4();
                break;
            case Cpu.CodeName.Matisse:
            case Cpu.CodeName.Vermeer:
                Socket_AM4_V2();
                break;
            case Cpu.CodeName.Renoir:
            case Cpu.CodeName.Lucienne:
            case Cpu.CodeName.Cezanne:
                Socket_FP6_AM4();
                break;
            case Cpu.CodeName.VanGogh:
                Socket_FF3();
                break;
            case Cpu.CodeName.Mendocino:
            case Cpu.CodeName.Rembrandt:
            case Cpu.CodeName.Phoenix:
            case Cpu.CodeName.Phoenix2:
            case Cpu.CodeName.DragonRange:
            case Cpu.CodeName.HawkPoint:
                Socket_FT6_FP7_FP8();
                break; 
            case Cpu.CodeName.GraniteRidge:
            case Cpu.CodeName.Genoa:
            case Cpu.CodeName.Bergamo:
            case Cpu.CodeName.Raphael:
                Socket_AM5_V1();
                break;
            default:
                return 0U; // Find the command by name
        }

        var matchingCommands = Commands?.Where(c => c.Item1 == "set-cogfx");
        var commands = matchingCommands!.ToList();
        if (commands.Count != 0)
        {
            return commands.Select(command => command.Item3).FirstOrDefault();
        }

        return 0U;
    }

    public static uint ReturnCoPer(Cpu.CodeName codeName)
    {
        Codename = codeName; //если класс неинициализирован - задать правильный Codename
        switch (Codename)
        {
            case Cpu.CodeName.SummitRidge:
            case Cpu.CodeName.PinnacleRidge:
                Socket_AM4_V1();
                break;
            case Cpu.CodeName.RavenRidge:
            case Cpu.CodeName.Picasso:
            case Cpu.CodeName.Dali:
            case Cpu.CodeName.FireFlight:
                Socket_FT5_FP5_AM4();
                break;
            case Cpu.CodeName.Matisse:
            case Cpu.CodeName.Vermeer:
                Socket_AM4_V2();
                break;
            case Cpu.CodeName.Renoir:
            case Cpu.CodeName.Lucienne:
            case Cpu.CodeName.Cezanne:
                Socket_FP6_AM4();
                break;
            case Cpu.CodeName.VanGogh:
                Socket_FF3();
                break;
            case Cpu.CodeName.Mendocino:
            case Cpu.CodeName.Rembrandt:
            case Cpu.CodeName.Phoenix:
            case Cpu.CodeName.Phoenix2:
            case Cpu.CodeName.DragonRange:
            case Cpu.CodeName.HawkPoint:
                Socket_FT6_FP7_FP8();
                break;
            case Cpu.CodeName.GraniteRidge:
            case Cpu.CodeName.Genoa:
            case Cpu.CodeName.Bergamo:
            case Cpu.CodeName.Raphael:
                Socket_AM5_V1();
                break;
            default:
                return 0U; // Find the command by name
        }

        var matchingCommands = Commands?.Where(c => c.Item1 == "set-coper");
        var commands = matchingCommands!.ToList();
        if (commands.Count != 0)
        {
            return commands.Select(command => command.Item3).FirstOrDefault();
        }

        return 0U;
    }

    /*Commands and addresses. Commands architecture basis from Universal x86 Tuning Utility. Its author is https://github.com/JamesCJ60, a lot of commands as well as various sources,
     * I just put them together and found some news.
     * Here are just a few of the authors who found SMU commands:
     https://github.com/JamesCJ60
     https://github.com/Erruar
     https://github.com/Irusanov
     https://github.com/FlyGoat */
    private static void Socket_FT5_FP5_AM4()
    {
        Mp1Cmd = 0x3B10528;
        Mp1Rsp = 0x3B10564;
        Mp1Arg = 0x3B10998;

        RsmuCmd = 0x3B10A20;
        RsmuRsp = 0x3B10A80;
        RsmuArg = 0x3B10A88;

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x6),
            ("stapm-limit", true, 0x1a),
            ("stapm-time", true, 0x1e),
            ("fast-limit", true, 0x1b),
            ("slow-limit", true, 0x1c),
            ("slow-time", true, 0x1d),
            ("tctl-temp", true, 0x1f),
            ("cHTC-temp", false, 0x56), // Use RSMU address
            ("vrm-current", true, 0x20),
            ("vrmmax-current", true, 0x22),
            ("vrmsoc-current", true, 0x21),
            ("vrmsocmax-current", true, 0x23),
            ("psi0-current", true, 0x24),
            ("psi0soc-current", true, 0x25),
            ("prochot-deassertion-ramp", true, 0x26),
            ("pbo-scalar", false, 0x68),
            ("power-saving", true, 0x19),
            ("max-performance", true, 0x18),
            ("oc-clk", false, 0x7d),
            ("oc-clk", true, 0x3C),
            ("oc-clk", true, 0x41),
            ("per-core-oc-clk", false, 0x7e),
            ("oc-volt", false, 0x7f),
            ("oc-volt", true, 0x40),
            ("enable-oc", false, 0x69),
            ("disable-oc", false, 0x6a),
            ("disable-oc", true, 0x3F),
            ("max-cpuclk", true, 0x44),
            ("min-cpuclk", true, 0x45),
            ("max-gfxclk", true, 0x46),
            ("min-gfxclk", true, 0x47),
            ("max-socclk-frequency", true, 0x48),
            ("min-socclk-frequency", true, 0x49),
            ("max-fclk-frequency", true, 0x4a),
            ("min-fclk-frequency", true, 0x4b),
            ("max-vcn", true, 0x4c),
            ("min-vcn", true, 0x4d),
            ("max-lclk", true, 0x4e),
            ("min-lclk", true, 0x4f),
            ("set-coper", false, 0x58),
            ("set-coall", false, 0x59),
            ("set-cogfx", false,
                0x59), //cuz Raven, Dali and Picasso have gfx voltage control in this command too but in different registers
            ("oc-volt-variable", false, 0x62), //For future updates
            ("update-skintemp-error", true, 0x27),
            ("setgpu-arerture-low", true, 0x28),
            ("setgpu-arerture-high", true, 0x29),
            ("start-gpu-link", true, 0x2A),
            ("stop-gpu-link", true, 0x2B),
            ("setcpu-freqto-ramstate", true, 0x2F),
            ("stopcpu-freqto-ramstate", true, 0x30),
            ("stopcpu-freqto-ramstate", true, 0x31),
            ("set-ulv-vid", true, 0x35),
            ("set-vddoff-vid", true, 0x3A),
            ("set-vmin-freq", true, 0x3B), //GFX minimum Curve Optimizer diapazon
            ("set-gpuclockoverdrive-byvid", true, 0x3D), //ONLY AM4!
            ("set-powergate-xgbe", true, 0x3E), //SUPER DANGEROUS!!! WILL NOT BE IN SAKU OVERCLOCK UI
            ("enable-cc6filter", true, 0x42)
        ];
    }

    private static void Socket_FP6_AM4()
    {
        Mp1Cmd = 0x3B10528;
        Mp1Rsp = 0x3B10564;
        Mp1Arg = 0x3B10998;

        RsmuCmd = 0x3B10A20;
        RsmuRsp = 0x3B10A80;
        RsmuArg = 0x3B10A88;

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x7),
            ("max-performance", true, 0x11),
            ("power-saving", true, 0x12),
            ("vrm-current", true, 0x1a),
            ("vrmmax-current", true, 0x1c),
            ("vrmsoc-current", true, 0x1b),
            ("vrmsocmax-current", true, 0x1d),
            ("psi0-current", true, 0x1e),
            ("psi0soc-current", true, 0x1f),
            ("stapm-limit", true, 0x14),
            ("fast-limit", true, 0x15),
            ("slow-limit", true, 0x16),
            ("slow-time", true, 0x17),
            ("stapm-time", true, 0x18),
            ("tctl-temp", true, 0x19),
            ("prochot-deassertion-ramp", true, 0x20),
            ("apu-slow-limit", true, 0x21),
            ("enable-oc", true, 0x2f),
            ("disable-oc", true, 0x30),
            ("oc-clk", true, 0x31),
            ("per-core-oc-clk", true, 0x32),
            ("oc-volt", true, 0x33),
            ("dgpu-skin-temp", true, 0x37),
            ("apu-skin-temp", true, 0x39),
            ("skin-temp-limit", true, 0x53), 
            ("enable-oc", false, 0x17), // Use RSMU address
            ("disable-oc", false, 0x18),
            ("oc-clk", false, 0x19),
            ("per-core-oc-clk", false, 0x1a),
            ("oc-volt", false, 0x1b),
            ("stapm-limit", false, 0x31),
            ("stapm-limit", false, 0x33),
            ("cHTC-temp", false, 0x37),
            ("pbo-scalar", false, 0x3F),
            ("set-cogfx", false, 0x57),
            ("set-coper", true, 0x54),
            ("set-coper", false, 0x52),
            ("set-coall", true, 0x55),
            ("set-cogfx", true, 0x64),
            ("set-coall", false, 0xB1),
            ("gfx-clk", false, 0x89),
        ];
    }

    private static void Socket_FT6_FP7_FP8()
    {
        if (Codename == Cpu.CodeName.DragonRange)
        {
            Mp1Cmd = 0x3010508;
            Mp1Rsp = 0x3010988;
            Mp1Arg = 0x3010984;
            RsmuCmd = 0x3B10524;
            RsmuRsp = 0x3B10570;
            RsmuArg = 0x3B10A40;
        }
        else
        {
            Mp1Cmd = 0x3B10528;
            Mp1Rsp = 0x3B10578;
            Mp1Arg = 0x3B10998;

            RsmuCmd = 0x3B10a20;
            RsmuRsp = 0x3B10a80;
            RsmuArg = 0x3B10a88;
        }

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x7),
            ("stapm-limit", true, 0x14),
            ("stapm-limit", false, 0x31), // Use RSMU address
            ("stapm-time", true, 0x18),
            ("fast-limit", true, 0x15),
            ("fast-limit", false, 0x32),
            ("slow-limit", true, 0x16),
            ("slow-limit", false, 0x33),
            ("slow-limit", false, 0x34),
            ("slow-time", true, 0x17),
            ("tctl-temp", true, 0x19),
            ("cHTC-temp", false, 0x37),
            ("apu-skin-temp", true, 0x33),
            ("apu-slow-limit", true, 0x23),
            ("vrm-current", true, 0x1a),
            ("vrmmax-current", true, 0x1c),
            ("vrmsoc-current", true, 0x1b),
            ("vrmsocmax-current", true, 0x1d),
            ("prochot-deassertion-ramp", true, 0x1f),
            ("gfx-clk", false, 0x89),
            ("dgpu-skin-temp", true, 0x32),
            ("power-saving", true, 0x12),
            ("max-performance", true, 0x11),
            ("pbo-scalar", false, 0x3E),
            ("oc-clk", false, 0x19),
            ("per-core-oc-clk", false, 0x1a),
            ("set-coall", false, 0x5d),
            ("set-coper", false, 0x53),
            ("set-coall", true, 0x4c),
            ("set-coper", true, 0x4b),
            ("set-cogfx", false, 0xb7),
            ("enable-oc", false, 0x17),
            ("disable-oc", false, 0x18)
        ];
    }

    private static void Socket_FF3()
    {
        Mp1Cmd = 0x3B10528;
        Mp1Rsp = 0x3B10578;
        Mp1Arg = 0x3B10998;

        RsmuCmd = 0x3B10a20;
        RsmuRsp = 0x3B10a80;
        RsmuArg = 0x3B10a88;

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x7),
            ("stapm-limit", true, 0x14),
            ("stapm-limit", false, 0x31), // Use RSMU address
            ("stapm-time", true, 0x18),
            ("fast-limit", true, 0x15),
            ("slow-limit", true, 0x16),
            ("slow-time", true, 0x17),
            ("tctl-temp", true, 0x19),
            ("cHTC-temp", false, 0x37),
            ("apu-skin-temp", true, 0x33),
            ("vrm-current", true, 0x1a),
            ("vrmmax-current", true, 0x1e),
            ("vrmsoc-current", true, 0x1b),
            ("vrmsocmax-current", true, 0x1d),
            ("vrmcvip-current", true, 0x1d),
            ("vrmgfx-current", true, 0x1c),
            ("vrmgfxmax-current", true, 0x1f),
            ("prochot-deassertion-ramp", true, 0x22),
            ("psi3cpu_current", true, 0x20),
            ("psi3gfx_current", true, 0x21),
            ("gfx-clk", false, 0x89),
            ("power-saving", true, 0x12),
            ("max-performance", true, 0x11),
            ("set-coall", true, 0x4c),
            ("set-coall", false, 0x5d),
            ("set-coper", true, 0x4b),
            ("set-cogfx", false, 0xb7)
        ];
    }

    private static void Socket_AM4_V1()
    {
        Mp1Cmd = 0X3B10528;
        Mp1Rsp = 0X3B10564;
        Mp1Arg = 0X3B10598;

        RsmuCmd = 0x3B1051C;
        RsmuRsp = 0X3B10568;
        RsmuArg = 0X3B10590;

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x6),
            ("stapm-limit", false, 0x64), // Use RSMU address✓
            ("vrm-current", false, 0x65), //✓
            ("vrmmax-current", false, 0x66), //✓
            ("tctl-temp", false, 0x68), //✓
            ("pbo-scalar", false, 0x6a), //
            ("oc-clk", false, 0x6c), //
            ("per-core-oc-clk", false, 0x6d), //✕
            ("oc-volt", false, 0x6e), //
            ("enable-oc", true, 0x23), //
            ("enable-oc", false, 0x6b), //
            ("disable-oc", true, 0x24), //
        ];
    }

    private static void Socket_AM4_V2()
    {
        Mp1Cmd = 0x3B10530;
        Mp1Rsp = 0x3B1057C;
        Mp1Arg = 0x3B109C4;

        RsmuCmd = 0x3B10524;
        RsmuRsp = 0x3B10570;
        RsmuArg = 0x3B10A40;

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x6),
            ("stapm-limit", true, 0x3D),
            ("stapm-limit", false, 0x53), // Use RSMU address
            ("vrm-current", true, 0x3B),
            ("vrm-current", false, 0x54),
            ("vrmmax-current", true, 0x3C),
            ("vrmmax-current", false, 0x55),
            ("tctl-temp", true, 0x23),
            ("tctl-temp", false, 0x56),
            ("pbo-scalar", false, 0x58),
            ("oc-clk", true, 0x26),
            ("oc-clk", false, 0x5C),
            ("per-core-oc-clk", true, 0x27),
            ("per-core-oc-clk", false, 0x5D),
            ("oc-volt", true, 0x28),
            ("oc-volt", false, 0x61),
            ("set-coall", true, 0x36),
            ("set-coper", true, 0x35),
            ("set-coall", false, 0xB),
            ("set-coper", false, 0xA), 
            ("enable-oc", true, 0x24),
            ("enable-oc", false, 0x5A),
            ("disable-oc", true, 0x25),
            ("disable-oc", false, 0x5B),
        ];
    }

    private static void Socket_AM5_V1()
    {
        Mp1Cmd = 0x3B10530;
        Mp1Rsp = 0x3B1057C;
        Mp1Arg = 0x3B109C4;

        RsmuCmd = 0x3B10524;
        RsmuRsp = 0x3B10570;
        RsmuArg = 0x3B10A40;

        Commands =
        [
            // Store the commands
            ("enable-feature", true, 0x5), // Use MP1 address
            ("disable-feature", true, 0x7),
            ("stapm-limit", true, 0x3e),
            ("stapm-limit", false, 0x56), // Use RSMU address
            ("vrm-current", true, 0x3c),
            ("vrm-current", false, 0x57),
            ("vrmmax-current", true, 0x3d),
            ("vrmmax-current", false, 0x58),
            ("tctl-temp", true, 0x3f),
            ("tctl-temp", false, 0x59),
            ("pbo-scalar", false, 0x5b),
            ("oc-clk", false, 0x5f),
            ("per-core-oc-clk", false, 0x60),
            ("oc-volt", false, 0x61),
            ("set-coall", false, 0x7),
            ("set-cogfx", false, 0x7),
            ("set-coper", false, 0x6),
            ("enable-oc", false, 0x5d),
            ("disable-oc", false, 0x5e),
        ];
    }
}