﻿using System.Diagnostics;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
using Saku_Overclock.Views;
using Windows.Foundation.Metadata;
using ZenStates.Core;
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.

namespace Saku_Overclock.SMUEngine;
/*Created by Serzhik Sakurazhima*/
/*Этот класс является частью программного обеспечения для управления разгоном процессоров.Он содержит методы и данные для отправки команд SMU (System Management Unit) на процессор.Вот краткое описание содержимого класса:

Приватные статические поля:

RSMU_RSP, RSMU_ARG, RSMU_CMD: содержат адреса для отправки команд SMU на системы AMD.
MP1_RSP, MP1_ARG, MP1_CMD: содержат адреса для отправки команд SMU на системы с другой архитектурой.
Публичные свойства и поля:

commands: список кортежей, хранящих информацию о командах (название, флаг применения, адрес).
ocmode: режим работы, определяемый настройками процессора.
cancelrange: флаг для отмены диапазона команд.
Методы:

OC_Detect: пытается определить, поддерживает ли процессор возможность разгона.
Init_OC_Mode: запускает процесс установки режима разгона.
SmuSettingsSave, SmuSettingsLoad, ProfileLoad, ConfigLoad, ConfigSave: методы для сохранения и загрузки настроек из JSON-файлов.
Play_Invernate_QuickSMU, ApplySettings, ApplyThis, SendRange: устаревшие методы, отвечающие за отправку команд SMU на процессор.
JsonRepair: метод для восстановления JSON-файлов в случае их повреждения.
CancelRange: устанавливает флаг отмены диапазона команд.
Приватные методы для определения адресов и отправки команд SMU на процессор.
Этот класс является частью большой системы, управляющей разгоном процессоров, и содержит функционал для загрузки/сохранения настроек, определения возможности разгона на данной архитектуре и отправки соответствующих команд на процессор.*/
internal class SendSMUCommand
{
    private static uint RSMU_RSP
    {
        get; set;
    }
    private static uint RSMU_ARG
    {
        get; set;
    }
    private static uint RSMU_CMD
    {
        get; set;
    }
    private static uint MP1_RSP
    {
        get; set;
    }
    private static uint MP1_ARG
    {
        get; set;
    }
    private static uint MP1_CMD
    {
        get; set;
    }
    public static List<(string, bool, uint)>? Commands
    {
        get; set;
    }
    public bool saveinfo = false;
    private Cpu? cpu;
    public static Cpu.CodeName Codename;
    public static string CPUCodenameString = string.Empty;
    private Smusettings smusettings = new();
    private Config config = new();
    private static JsonContainers.Notifications notify = new();
    private readonly ZenStates.Core.Mailbox testMailbox = new();
    private Profile[] profile = new Profile[1];
    public string? ocmode;
    private bool cancelrange = false;
    private bool dangersettingsapplied = false;
    private string checkAdjLine = "nothing to show. Hehe";
    public static bool SafeReapply { get; set; } = true;

    public SendSMUCommand()
    {
        try
        {
            cpu ??= CpuSingleton.GetInstance();
            Codename = cpu.info.codeName;
        }
        catch (Exception ex)
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
            TraceIt_TraceError(ex.ToString());
        }
        try
        {
            ConfigLoad();
            if (config.ReapplySafeOverclock) { SafeReapply = true; } else { SafeReapply = false; }
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static bool OC_Detect(SendSMUCommand sendcpu) // На неподдерживаемом оборудовании мгновенно отключит эти настройки
    {
        try
        {
            _ = sendcpu.Init_OC_Mode();
            if (sendcpu.ocmode == "set_enable_oc is not supported on this family")
            {
                return false;
            }
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); return true; }
        return false;
    }
    public async Task Init_OC_Mode()
    {
        ocmode = "";
        var p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"ryzenadj.exe";
        p.StartInfo.Arguments = "--enable-oc";
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.Start();
        var outputWriter = p.StandardOutput;
        var line = outputWriter.ReadLine();
        if (line != "")
        {
            ocmode = line;
        }
        await p.WaitForExitAsync();
    }
    //JSON
    public void SmuSettingsSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
        }
        catch
        {
            // ignored
        }
    }
    public void SmuSettingsLoad()
    {
        try
        {
            smusettings = JsonConvert.DeserializeObject<Smusettings>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json"));
        }
        catch (Exception ex) { JsonRepair('s'); TraceIt_TraceError(ex.ToString()); }
    }

    public void ProfileLoad()
    {
        try
        {

            profile = JsonConvert.DeserializeObject<Profile[]>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json"));
        }
        catch (Exception ex) { JsonRepair('p'); TraceIt_TraceError(ex.ToString()); }
    }
    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
        }
        catch (Exception ex) { JsonRepair('c'); TraceIt_TraceError(ex.ToString()); }
    }
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config, Formatting.Indented));
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static async void NotifyLoad()
    {
        var success = false;
        var retryCount = 1;
        while (!success && retryCount < 3)
        {
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))
            {
                try
                {
                    notify = JsonConvert.DeserializeObject<JsonContainers.Notifications>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json"))!;
                    if (notify != null) { success = true; } else { RepairTraceIt_Notify(); }
                }
                catch { RepairTraceIt_Notify(); }
            }
            else { RepairTraceIt_Notify(); }
            if (!success)
            {
                await Task.Delay(30);
                retryCount++;
            }
        }
    }
    public static void NotifySave()
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify, Formatting.Indented));
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    private static void RepairTraceIt_Notify()
    {
        try
        {
            notify = new JsonContainers.Notifications();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
            App.MainWindow.Close();
        }
        if (notify != null)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
            }
            catch
            {
                File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json");
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
        }
        else
        {
            try
            {

                File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json");
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\notify.json", JsonConvert.SerializeObject(notify));
                App.MainWindow.Close();
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
        }
    }
    public void JsonRepair(char file)
    {
        if (file == 'c')
        {
            try
            {
                config = new Config();
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (config != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {

                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        } 
        if (file == 's')
        {
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    smusettings = new Smusettings();
                }
            }
            catch
            {
                App.MainWindow.Close();
            }
            if (smusettings != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smusettings.json", JsonConvert.SerializeObject(smusettings, Formatting.Indented));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
        }
        if (file == 'p')
        {
            try
            {
                for (var j = 0; j < 3; j++)
                {
                    profile[j] = new Profile();
                }
            }
            catch
            {
                App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                App.MainWindow.Close();
            }
            if (profile != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\profile.json", JsonConvert.SerializeObject(profile));
                    App.MainWindow.Close();
                }
                catch
                {
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
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
        ConfigLoad();
        if (smusettings.QuickSMUCommands == null)
        {
            return;
        }
        if (config.Preset != -1)
        {
            if (profile[config.Preset].smuEnabled == false) { return; }
        }
        for (var i = 0; i < smusettings.QuickSMUCommands.Count; i++)
        {
            if (mode == 0)
            {
                if (smusettings.QuickSMUCommands[i].ApplyWith)
                {
                    ApplySettings(i);
                }
            }
            else
            {
                if (smusettings.QuickSMUCommands[i].ApplyWith || smusettings.QuickSMUCommands[i].Startup)
                {
                    ApplySettings(i);
                }
            }
        }
    }
    public void ApplySettings(int CommandIndex)
    {
        try
        {
            ZenStates.Core.Mailbox quickMailbox1 = new();
            uint[]? args;
            string[]? userArgs;
            uint addrMsg;
            uint addrRsp;
            uint addrArg;
            uint command;
            SmuSettingsLoad();
            args = Utils.MakeCmdArgs();
            userArgs = smusettings?.QuickSMUCommands?[CommandIndex].Argument.Trim().Split(',');
            TryConvertToUint(smusettings?.MailBoxes?[smusettings.QuickSMUCommands![CommandIndex].MailIndex].CMD!, out addrMsg);
            TryConvertToUint(smusettings?.MailBoxes?[smusettings.QuickSMUCommands![CommandIndex].MailIndex].RSP!, out addrRsp);
            TryConvertToUint(smusettings?.MailBoxes?[smusettings.QuickSMUCommands![CommandIndex].MailIndex].ARG!, out addrArg);
            TryConvertToUint(smusettings?.QuickSMUCommands?[CommandIndex].Command!, out command);
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
            var status = cpu?.smu.SendSmuCommand(quickMailbox1, command, ref args);
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }

    //From RyzenADJ string to SMU Calls 
    public async void Translate(string _ryzenAdjString, bool save)
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

        saveinfo = save;
        try
        {
            cpu ??= CpuSingleton.GetInstance();
            if (cpu != null && Codename != cpu.info.codeName || !CPUCodenameString.Contains(cpu!.info.codeName.ToString())) { Codename = cpu.info.codeName; CPUCodenameString = Codename.ToString(); if (CPUCodenameString == string.Empty) { CPUCodenameString = RyzenADJWrapper.GetCPUCodename(); } }
            if (cpu?.info.codeName == Cpu.CodeName.SummitRidge || Codename == Cpu.CodeName.SummitRidge ||
                cpu?.info.codeName == Cpu.CodeName.PinnacleRidge) { Socket_AM4_V1(); }
            else if (cpu?.info.codeName == Cpu.CodeName.RavenRidge || CPUCodenameString.Contains("RAVEN") ||
                cpu?.info.codeName == Cpu.CodeName.Picasso || CPUCodenameString.Contains("PICASSO") ||
                cpu?.info.codeName == Cpu.CodeName.Dali || CPUCodenameString.Contains("DALI") ||
                /*cpu.info.codeName == Cpu.CodeName.Pollock || */
                cpu?.info.codeName == Cpu.CodeName.FireFlight) { Socket_FT5_FP5_AM4(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Matisse || 
                cpu?.info.codeName == Cpu.CodeName.Vermeer) { Socket_AM4_V2(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Renoir || CPUCodenameString.Contains("RENOIR") ||
                cpu?.info.codeName == Cpu.CodeName.Lucienne || CPUCodenameString.Contains("LUCIENNE") || CPUCodenameString.Contains("CEZANNE") ||
                cpu?.info.codeName == Cpu.CodeName.Cezanne) { Socket_FP6_AM4(); }
            else if (cpu?.info.codeName == Cpu.CodeName.VanGogh || CPUCodenameString.Contains("VANGOGH")) { Socket_FF3(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Mendocino || CPUCodenameString.Contains("MENDOCINO") ||
                cpu?.info.codeName == Cpu.CodeName.Rembrandt || CPUCodenameString.Contains("REMBRANDT") ||
                cpu?.info.codeName == Cpu.CodeName.Phoenix || CPUCodenameString.Contains("PHOENIX") ||
                cpu?.info.codeName == Cpu.CodeName.Phoenix2 || CPUCodenameString.Contains("HAWKPOINT") ||
                cpu?.info.codeName == Cpu.CodeName.StrixPoint || CPUCodenameString.Contains("STRIXPOINT") ||
                cpu?.info.codeName == Cpu.CodeName.DragonRange) { Socket_FT6_FP7_FP8(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Raphael /*||
                 cpu.info.codeName == Cpu.CodeName.GraniteRidge*/) { Socket_AM5_V1(); } //Не всё поддерживается, но это будет в будущем исправлено
            else
            {  
                MP1_CMD = 0x3B10528;
                MP1_RSP = 0x3B10564;
                MP1_ARG = 0x3B10998;
                RSMU_CMD = 0x3B10A20;
                RSMU_RSP = 0x3B10A80;
                RSMU_ARG = 0x3B10A88;
            }
            //Убрать последний знак в строке аргументов для применения
            _ryzenAdjString = _ryzenAdjString.TrimEnd();
            //Разделить команды в Array
            var ryzenAdjCommands = _ryzenAdjString.Split(' ');
            ryzenAdjCommands = ryzenAdjCommands.Distinct().ToArray();
            //Выполнить через Array
            foreach (var ryzenAdjCommand in ryzenAdjCommands)
            {
                await Task.Run(() =>
                {
                    try
                    {
                        //Проверить есть ли совпадения с листом опасных команд, которые нежелательно переприменять, чтобы не получить краш системы из-за перегрузки SMU
                        if (checkAdjLine == _ryzenAdjString  /*Если прошлое применение совпадает с текущим применением*/
                        && dangersettingsapplied /*Если уже были применены опасные настройки*/
                        && !save /*Если пользователь сам их не выставляет*/
                        && SafeReapply /*Если включено безопасное применение*/
                        && terminateCommands.Any(tc => ryzenAdjCommand.Contains(tc))) //Если есть совпадения в командах
                        {
                            //Ничего не делать 
                            //TraceIt_TraceError("Вы хотите применить опасные параметры?");
                        }
                        else
                        {
                            //TraceIt_TraceError("Применены безопасные параметры?\n" + _ryzenAdjString);
                            var command = ryzenAdjCommand;
                            if (!command.Contains('=')) { command = ryzenAdjCommand + "=0"; }
                            //Выяснить какая команда стоит до знака равно
                            var ryzenAdjCommandString = command.Split('=')[0].Replace("=", null).Replace("--", null);
                            if (command[(ryzenAdjCommand.IndexOf('=') + 1)..].Contains(',')) //Если это составная команда с не нулевым аргументом
                            {
                                var parts = command[(ryzenAdjCommand.IndexOf('=') + 1)..].Split(','); //узнать первый аргумент, разделив аргументы
                                if (parts.Length == 2 && uint.TryParse(parts[1], out var commaValue))
                                {
                                    ApplySettings(ryzenAdjCommandString, 0x0, commaValue); //Применить преимущественно второй аргумент
                                }
                            }
                            else
                            {
                                //А теперь узнаем что стоит после знака равно
                                var ryzenAdjCommandValueString = command[(ryzenAdjCommand.IndexOf('=') + 1)..];
                                //Конвертировать в Uint
                                if (ryzenAdjCommandValueString.Contains('=')) { ryzenAdjCommandValueString = ryzenAdjCommandValueString.Replace("=", null); }
                                var ryzenAdjCommandValue = Convert.ToUInt32(ryzenAdjCommandValueString);
                                if (ryzenAdjCommandValue <= 0 && !ryzenAdjCommandString.Contains("coall") && !ryzenAdjCommandString.Contains("coper") && !ryzenAdjCommandString.Contains("cogfx"))
                                {
                                    ApplySettings(ryzenAdjCommandString, 0x0); //Если пользователь редактировал конфиги вручную, чтобы программа не крашнулась из-за непредвиденного отрицательного значения
                                }
                                else { ApplySettings(ryzenAdjCommandString, ryzenAdjCommandValue); }
                            }
                        }
                        Task.Delay(50); //Задержка перед применением следующей группы команд
                    }
                    catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
                });
            }
            dangersettingsapplied = true; saveinfo = false;
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
        checkAdjLine = _ryzenAdjString;
    }

    public void ApplySettings(string commandName, uint value, uint value1 = 0)
    {
        try
        {
            var Args = new uint[6];
            Args[0] = value;
            if (value1 != 0)
            {
                Args[1] = value1; //Если команда составная
            }
            //Найти код команды по имени
            var matchingCommands = Commands?.Where(c => c.Item1 == commandName);
            if (matchingCommands?.Any() == true)
            {
                var tasks = new List<Task>();
                foreach (var command in matchingCommands)
                {
                    tasks.Add(Task.Run(() =>
                    {
                        //Применить уже эту команду наконец-то!
                        if (command.Item2 == true) { ApplyThis(1, command.Item3, Args, command.Item1); }
                        else { ApplyThis(0, command.Item3, Args, command.Item1); }
                    }));
                }

                Task.WaitAll([.. tasks]);
            }
            else
            {
                if (!$"\nCommand '{commandName}' not found".Contains("Command '' not found"))
                {
                    ПараметрыPage.ApplyInfo += $"\nCommand '{commandName}' not found";
                }
            }
        }
        catch (Exception ex)
        {
            TraceIt_TraceError(ex.ToString());
            ПараметрыPage.ApplyInfo += $"\nCommand '{commandName}' not found";
        }
    }

    public void ApplyThis(int Mailbox, uint Command, uint[] args, string CommandName)
    {
        try
        {
            cpu ??= CpuSingleton.GetInstance();
            if (cpu != null && Codename != cpu.info.codeName || !CPUCodenameString.Contains(cpu!.info.codeName.ToString())) { Codename = cpu.info.codeName; CPUCodenameString = Codename.ToString(); if (CPUCodenameString == string.Empty) { CPUCodenameString = RyzenADJWrapper.GetCPUCodename(); } }
            if (cpu?.info.codeName == Cpu.CodeName.SummitRidge || Codename == Cpu.CodeName.SummitRidge ||
                cpu?.info.codeName == Cpu.CodeName.PinnacleRidge) { Socket_AM4_V1(); }
            else if (cpu?.info.codeName == Cpu.CodeName.RavenRidge || CPUCodenameString.Contains("RAVEN") ||
                cpu?.info.codeName == Cpu.CodeName.Picasso || CPUCodenameString.Contains("PICASSO") ||
                cpu?.info.codeName == Cpu.CodeName.Dali || CPUCodenameString.Contains("DALI") ||
                /*cpu.info.codeName == Cpu.CodeName.Pollock || */
                cpu?.info.codeName == Cpu.CodeName.FireFlight) { Socket_FT5_FP5_AM4(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Matisse ||
                cpu?.info.codeName == Cpu.CodeName.Vermeer) { Socket_AM4_V2(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Renoir || CPUCodenameString.Contains("RENOIR") ||
                cpu?.info.codeName == Cpu.CodeName.Lucienne || CPUCodenameString.Contains("LUCIENNE") || CPUCodenameString.Contains("CEZANNE") ||
                cpu?.info.codeName == Cpu.CodeName.Cezanne) { Socket_FP6_AM4(); }
            else if (cpu?.info.codeName == Cpu.CodeName.VanGogh || CPUCodenameString.Contains("VANGOGH")) { Socket_FF3(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Mendocino || CPUCodenameString.Contains("MENDOCINO") ||
                cpu?.info.codeName == Cpu.CodeName.Rembrandt || CPUCodenameString.Contains("REMBRANDT") ||
                cpu?.info.codeName == Cpu.CodeName.Phoenix || CPUCodenameString.Contains("PHOENIX") ||
                cpu?.info.codeName == Cpu.CodeName.Phoenix2 || CPUCodenameString.Contains("HAWKPOINT") ||
                cpu?.info.codeName == Cpu.CodeName.StrixPoint || CPUCodenameString.Contains("STRIXPOINT") ||
                cpu?.info.codeName == Cpu.CodeName.DragonRange) { Socket_FT6_FP7_FP8(); }
            else if (cpu?.info.codeName == Cpu.CodeName.Raphael /*||
                 cpu.info.codeName == Cpu.CodeName.GraniteRidge*/) { Socket_AM5_V1(); } //Не всё поддерживается, но это будет в будущем исправлено
            else
            {
                MP1_CMD = 0x3B10528;
                MP1_RSP = 0x3B10564;
                MP1_ARG = 0x3B10998;
                RSMU_CMD = 0x3B10A20;
                RSMU_RSP = 0x3B10A80;
                RSMU_ARG = 0x3B10A88;
            }
            uint addrMsg;
            uint addrRsp;
            uint addrArg;
            if (Mailbox == 0)
            {
                addrMsg = RSMU_CMD;
                addrRsp = RSMU_RSP;
                addrArg = RSMU_ARG;
            }
            else
            {
                addrMsg = MP1_CMD;
                addrRsp = MP1_RSP;
                addrArg = MP1_ARG;
            }
            testMailbox.SMU_ADDR_MSG = addrMsg;
            testMailbox.SMU_ADDR_RSP = addrRsp;
            testMailbox.SMU_ADDR_ARG = addrArg;
            if (!saveinfo && CommandName == "stopcpu-freqto-ramstate") { return; } //Чтобы уж точно не осталось в RyzenADJline, так как может крашнуть систему
            var status = cpu?.smu.SendSmuCommand(testMailbox, Command, ref args);
            if (status != SMU.Status.OK) { ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + CommandName + "Param_SMU_Command_Status".GetLocalized() + status ; } //Если при применении что-то пошло не так - сказать об ошибке
        }
        catch
        {
            ПараметрыPage.ApplyInfo += "\n" + "Param_SMU_Command".GetLocalized() + CommandName + "Param_SMU_Command_Error".GetLocalized();
        }
    }
    private string CommandNameParser(string CommandName)
    { 
        var a = CommandName switch 
        {
            "enable-feature" => "".GetLocalized(),
            "disable-feature" => "".GetLocalized(),
            "stapm-limit" => "".GetLocalized(),
            "vrm-current" => "".GetLocalized(),
            "vrmmax-current" => "".GetLocalized(),
            "tctl-temp" => "".GetLocalized(),
            "pbo-scalar" => "".GetLocalized(),
            "oc-clk" => "".GetLocalized(),
            "per-core-oc-clk" => "".GetLocalized(),
            "oc-volt" => "".GetLocalized(),
            "set-coall" => "".GetLocalized(),
            "set-cogfx" => "".GetLocalized(),
            "set-coper" => "".GetLocalized(),
            "enable-oc" => "".GetLocalized(),
            "disable-oc" => "".GetLocalized(),
            "stapm-time" => "".GetLocalized(),
            "fast-limit" => "".GetLocalized(),
            "slow-limit" => "".GetLocalized(),
            "slow-time" => "".GetLocalized(),
            "cHTC-temp" => "".GetLocalized(),
            "apu-skin-temp" => "".GetLocalized(),
            "vrmsoc-current" => "".GetLocalized(),
            "vrmsocmax-current" => "".GetLocalized(),
            "vrmcvip-current" => "".GetLocalized(),
            "vrmgfx-current" => "".GetLocalized(),
            "vrmgfxmax-current" => "".GetLocalized(),
            "prochot-deassertion-ramp" => "".GetLocalized(),
            "psi3cpu_current" => "".GetLocalized(),
            "psi3gfx_current" => "".GetLocalized(),
            "gfx-clk" => "".GetLocalized(),
            "power-saving" => "".GetLocalized(),
            "max-performance" => "".GetLocalized(),
            "apu-slow-limit" => "".GetLocalized(),
            "dgpu-skin-temp" => "".GetLocalized(),
            "psi0-current" => "".GetLocalized(),
            "psi0soc-current" => "".GetLocalized(),
            "skin-temp-limit" => "".GetLocalized(),
            "max-cpuclk" => "".GetLocalized(),
            "min-cpuclk" => "".GetLocalized(),
            "max-gfxclk" => "".GetLocalized(),
            "min-gfxclk" => "".GetLocalized(),
            "max-socclk-frequency" => "".GetLocalized(),
            "min-socclk-frequency" => "".GetLocalized(),
            "max-fclk-frequency" => "".GetLocalized(),
            "min-fclk-frequency" => "".GetLocalized(),
            "max-vcn" => "".GetLocalized(),
            "min-vcn" => "".GetLocalized(),
            "max-lclk" => "".GetLocalized(),
            "min-lclk" => "".GetLocalized(),
            "oc-volt-variable" => "".GetLocalized(),
            "update-skintemp-error" => "".GetLocalized(),
            "setgpu-arerture-low" => "".GetLocalized(),
            "setgpu-arerture-high" => "".GetLocalized(),
            "start-gpu-link" => "".GetLocalized(),
            "stop-gpu-link" => "".GetLocalized(),
            "setcpu-freqto-ramstate" => "".GetLocalized(),
            "stopcpu-freqto-ramstate" => "".GetLocalized(), 
            "set-ulv-vid" => "".GetLocalized(),
            "set-vddoff-vid" => "".GetLocalized(),
            "set-vmin-freq" => "".GetLocalized(),
            "set-gpuclockoverdrive-byvid" => "".GetLocalized(),
            "set-powergate-xgbe" => "".GetLocalized(),
            "enable-cc6filter" => "".GetLocalized(),
            _ => "1"
        };
        return a;
    }
    public static void TraceIt_TraceError(string error) //Система TraceIt! позволит логгировать все ошибки
    {
        if (error != string.Empty)
        {
            NotifyLoad(); //Добавить уведомление
            notify.Notifies ??= [];
            notify.Notifies.Add(new Notify { Title = "TraceIt_Error".GetLocalized(), Msg = error, Type = InfoBarSeverity.Error });
            NotifySave();
        }
    }
    public void CancelRange()
    {
        cancelrange = true;
    }
    public async void SendRange(string CommandIndex, string StartIndex, string EndIndex, int Mailbox, bool Log)
    {
        cancelrange = false;
        try
        {
            await Task.Run(() =>
            {
                uint startes;
                uint endes;
                TryConvertToUint(StartIndex, out startes);
                TryConvertToUint(EndIndex, out endes);
                if (startes == endes) { startes = 0; endes = uint.MaxValue; }
                var logFilePath = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\smurangelog.txt";
                using var sw = new StreamWriter(logFilePath, true);
                if (Log)
                {
                    if (!File.Exists(logFilePath)) { sw.WriteLine("//------SMU LOG------\\\\"); }
                    sw.WriteLine($"{DateTime.Now:HH:mm:ss} | Date: {DateTime.Now:dd.MM.yyyy} | MailBox: {Mailbox} | CMD: {CommandIndex} | Range: {StartIndex}-{EndIndex}");
                }
                SmuSettingsLoad();
                for (var j = startes; j < endes; j++)
                {
                    if (cancelrange) { cancelrange = false; sw.WriteLine("//------CANCEL------\\\\"); return; }
                    uint[]? args;
                    uint addrMsg;
                    uint addrRsp;
                    uint addrArg;
                    uint command;
                    args = Utils.MakeCmdArgs();
                    TryConvertToUint(smusettings?.MailBoxes![Mailbox].CMD!, out addrMsg);
                    TryConvertToUint(smusettings?.MailBoxes![Mailbox].RSP!, out addrRsp);
                    TryConvertToUint(smusettings?.MailBoxes![Mailbox].ARG!, out addrArg);
                    TryConvertToUint(CommandIndex, out command);
                    testMailbox.SMU_ADDR_MSG = addrMsg;
                    testMailbox.SMU_ADDR_RSP = addrRsp;
                    testMailbox.SMU_ADDR_ARG = addrArg;
                    args[0] = j;
                    try
                    {
                        var status = cpu?.smu.SendSmuCommand(testMailbox, command, ref args);
                        if (Log) { sw.WriteLine($"{DateTime.Now:HH:mm:ss} | MailBox: {Mailbox} | CMD: {command:X} | Arg: {j:X} | Status: {status}"); }
                    }
                    catch (Exception ex)
                    {
                        if (Log) { sw.WriteLine($"{DateTime.Now:HH:mm:ss} | MailBox: {Mailbox} | CMD: {command:X} | Arg: {j:X} | Status: {ex.Message}"); }
                    }
                }
                // ConfigLoad(); config.RangeApplied = true; ConfigSave(); 
                if (Log) { sw.WriteLine("//------OK------\\\\"); }
            });
        }
        catch (Exception ex) { TraceIt_TraceError(ex.ToString()); }
    }
    public static uint ReturnCoGFX(Cpu.CodeName codeName)
    {
        Codename = codeName; //если класс неинициализирован - задать правильный Codename
        if (Codename == Cpu.CodeName.SummitRidge || Codename == Cpu.CodeName.PinnacleRidge) { Socket_AM4_V1(); }
        else if (Codename == Cpu.CodeName.RavenRidge || Codename == Cpu.CodeName.Picasso || Codename == Cpu.CodeName.Dali || /*cpu.info.codeName == Cpu.CodeName.Pollock || */ Codename == Cpu.CodeName.FireFlight) { Socket_FT5_FP5_AM4(); }
        else if (Codename == Cpu.CodeName.Matisse || Codename == Cpu.CodeName.Vermeer) { Socket_AM4_V2(); }
        else if (Codename == Cpu.CodeName.Renoir || Codename == Cpu.CodeName.Lucienne || Codename == Cpu.CodeName.Cezanne) { Socket_FP6_AM4(); }
        else if (Codename == Cpu.CodeName.VanGogh) { Socket_FF3(); }
        else if (Codename == Cpu.CodeName.Mendocino || Codename == Cpu.CodeName.Rembrandt || Codename == Cpu.CodeName.Phoenix || Codename == Cpu.CodeName.Phoenix2 /*|| cpu.info.codeName == Cpu.CodeName.Strix*/ || Codename == Cpu.CodeName.DragonRange || Codename == Cpu.CodeName.HawkPoint) { Socket_FT6_FP7_FP8(); }
        else if (Codename == Cpu.CodeName.Raphael /*|| cpu.info.codeName == Cpu.CodeName.GraniteRidge*/) { Socket_AM5_V1(); }
        else { return 0U; }  // Find the command by name
        var matchingCommands = Commands?.Where(c => c.Item1 == "set-cogfx");
        if (matchingCommands?.Any() == true)
        {
            foreach (var command in matchingCommands)
            {
                return command.Item3;
            }
        }
        return 0U;
    }
    public static uint ReturnCoPer(Cpu.CodeName codeName)
    {
        Codename = codeName; //если класс неинициализирован - задать правильный Codename
        if (Codename == Cpu.CodeName.SummitRidge || Codename == Cpu.CodeName.PinnacleRidge) { Socket_AM4_V1(); }
        else if (Codename == Cpu.CodeName.RavenRidge || Codename == Cpu.CodeName.Picasso || Codename == Cpu.CodeName.Dali || /*cpu.info.codeName == Cpu.CodeName.Pollock || */ Codename == Cpu.CodeName.FireFlight) { Socket_FT5_FP5_AM4(); }
        else if (Codename == Cpu.CodeName.Matisse || Codename == Cpu.CodeName.Vermeer) { Socket_AM4_V2(); }
        else if (Codename == Cpu.CodeName.Renoir || Codename == Cpu.CodeName.Lucienne || Codename == Cpu.CodeName.Cezanne) { Socket_FP6_AM4(); }
        else if (Codename == Cpu.CodeName.VanGogh) { Socket_FF3(); }
        else if (Codename == Cpu.CodeName.Mendocino || Codename == Cpu.CodeName.Rembrandt || Codename == Cpu.CodeName.Phoenix || Codename == Cpu.CodeName.Phoenix2 /*|| cpu.info.codeName == Cpu.CodeName.Strix*/ || Codename == Cpu.CodeName.DragonRange || Codename == Cpu.CodeName.HawkPoint) { Socket_FT6_FP7_FP8(); }
        else if (Codename == Cpu.CodeName.Raphael /*|| cpu.info.codeName == Cpu.CodeName.GraniteRidge*/) { Socket_AM5_V1(); }
        else { return 0U; }  // Find the command by name
        var matchingCommands = Commands?.Where(c => c.Item1 == "set-coper");
        if (matchingCommands?.Any() == true)
        {
            foreach (var command in matchingCommands)
            {
                return command.Item3;
            }
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
        MP1_CMD = 0x3B10528;
        MP1_RSP = 0x3B10564;
        MP1_ARG = 0x3B10998;

        RSMU_CMD = 0x3B10A20;
        RSMU_RSP = 0x3B10A80;
        RSMU_ARG = 0x3B10A88;

        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x6),
                ("stapm-limit",true, 0x1a),
                ("stapm-time",true , 0x1e),
                ("fast-limit",true , 0x1b),
                ("slow-limit",true , 0x1c),
                ("slow-time",true , 0x1d),
                ("tctl-temp",true , 0x1f),
                ("cHTC-temp",false , 0x56), // Use RSMU address
                ("vrm-current",true , 0x20),
                ("vrmmax-current",true , 0x22),
                ("vrmsoc-current",true , 0x21),
                ("vrmsocmax-current",true , 0x23),
                ("psi0-current",true , 0x24),
                ("psi0soc-current",true , 0x25),
                ("prochot-deassertion-ramp",true , 0x26),
                ("pbo-scalar",false , 0x68),
                ("power-saving",true , 0x19),
                ("max-performance",true , 0x18),
                ("oc-clk",false , 0x7d),
                ("oc-clk", true , 0x3C),
                ("oc-clk", true , 0x41),
                ("per-core-oc-clk",false , 0x7e),
                ("oc-volt",false , 0x7f),
                ("oc-volt", true , 0x40),
                ("enable-oc",false , 0x69),
                ("disable-oc",false , 0x6a),
                ("disable-oc", true , 0x3F),
                ("max-cpuclk",true, 0x44),
                ("min-cpuclk",true, 0x45),
                ("max-gfxclk",true, 0x46),
                ("min-gfxclk",true, 0x47),
                ("max-socclk-frequency",true, 0x48),
                ("min-socclk-frequency",true, 0x49),
                ("max-fclk-frequency",true, 0x4a),
                ("min-fclk-frequency",true, 0x4b),
                ("max-vcn",true, 0x4c),
                ("min-vcn",true, 0x4d),
                ("max-lclk",true, 0x4e),
                ("min-lclk",true, 0x4f),
                ("set-coper",false , 0x58),
                ("set-coall",false , 0x59),
                ("set-cogfx",false , 0x59), //cuz Raven, Dali and Picasso have gfx voltage control in this command too but in different registers
                ("oc-volt-variable",false, 0x62), //For future updates
                ("update-skintemp-error", true, 0x27),
                ("setgpu-arerture-low", true, 0x28),
                ("setgpu-arerture-high", true , 0x29),
                ("start-gpu-link", true , 0x2A),
                ("stop-gpu-link", true , 0x2B),
                ("setcpu-freqto-ramstate", true , 0x2F),
                ("stopcpu-freqto-ramstate", true , 0x30),
                ("stopcpu-freqto-ramstate", true , 0x31),
                ("set-ulv-vid", true , 0x35),
                ("set-vddoff-vid", true , 0x3A),
                ("set-vmin-freq", true , 0x3B),  //GFX minimum Curve Optimizer diapazon
                ("set-gpuclockoverdrive-byvid", true , 0x3D), //ONLY AM4!
                ("set-powergate-xgbe", true , 0x3E), //SUPER DANGEROUS!!! WILL NOT BE IN SAKU OVERCLOCK UI
                ("enable-cc6filter",  true , 0x42)
            ];
    }

    private static void Socket_FP6_AM4()
    {
        MP1_CMD = 0x3B10528;
        MP1_RSP = 0x3B10564;
        MP1_ARG = 0x3B10998;

        RSMU_CMD = 0x3B10A20;
        RSMU_RSP = 0x3B10A80;
        RSMU_ARG = 0x3B10A88;

        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x7),
                ("max-performance",true , 0x11),
                ("power-saving",true , 0x12),
                ("vrm-current",true , 0x1a),
                ("vrmmax-current",true , 0x1c),
                ("vrmsoc-current",true , 0x1b),
                ("vrmsocmax-current",true , 0x1d),
                ("psi0-current",true , 0x1e),
                ("psi0soc-current",true , 0x1f),
                ("stapm-limit",true , 0x14),
                ("fast-limit",true , 0x15),
                ("slow-limit",true , 0x16),
                ("slow-time",true , 0x17),
                ("stapm-time",true , 0x18),
                ("tctl-temp",true , 0x19),
                ("prochot-deassertion-ramp",true , 0x20),
                ("apu-slow-limit",true , 0x21),
                ("enable-oc",true , 0x2f),
                ("disable-oc",true , 0x30),
                ("oc-clk",true , 0x31),
                ("per-core-oc-clk",true , 0x32),
                ("oc-volt",true , 0x33),
                ("dgpu-skin-temp",true , 0x37),
                ("apu-skin-temp",true , 0x39),
                ("skin-temp-limit",true , 0x53),
                ("set-coper",true , 0x54),
                ("set-coall",true , 0x55),
                ("set-cogfx",true , 0x64),
                ("enable-oc",false , 0x17), // Use RSMU address
                ("disable-oc",false , 0x18),
                ("oc-clk",false , 0x19),
                ("per-core-oc-clk",false , 0x1a),
                ("oc-volt",false , 0x1b),
                ("stapm-limit",false , 0x31),
                ("stapm-limit",false , 0x33),
                ("cHTC-temp",false , 0x37),
                ("pbo-scalar",false , 0x3F),
                ("set-cogfx",false , 0x57),
                ("gfx-clk",false , 0x89),
                ("set-coall",false , 0xB1)
            ];
    }

    private static void Socket_FT6_FP7_FP8()
    {
        if (Codename == Cpu.CodeName.DragonRange)
        {
            MP1_CMD = 0x3010508;
            MP1_RSP = 0x3010988;
            MP1_ARG = 0x3010984;
            RSMU_CMD = 0x3B10524;
            RSMU_RSP = 0x3B10570;
            RSMU_ARG = 0x3B10A40;
        }
        else
        {
            MP1_CMD = 0x3B10528;
            MP1_RSP = 0x3B10578;
            MP1_ARG = 0x3B10998;

            RSMU_CMD = 0x3B10a20;
            RSMU_RSP = 0x3B10a80;
            RSMU_ARG = 0x3B10a88;
        }
        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x7),
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
                ("apu-slow-limit",true , 0x23),
                ("vrm-current", true, 0x1a),
                ("vrmmax-current", true, 0x1c),
                ("vrmsoc-current", true, 0x1b),
                ("vrmsocmax-current", true ,0x1d),
                ("prochot-deassertion-ramp", true, 0x1f),
                ("gfx-clk", false, 0x89),
                ("dgpu-skin-temp", true, 0x32),
                ("power-saving", true, 0x12),
                ("max-performance", true, 0x11),
                ("pbo-scalar", false, 0x3E),
                ("oc-clk",  false, 0x19),
                ("per-core-oc-clk", false, 0x1a),
                ("set-coall",   true, 0x4c),
                ("set-coall",   false, 0x5d),
                ("set-coper",   true, 0x4b),
                ("set-cogfx",   false, 0xb7),
                ("enable-oc",   false, 0x17),
                ("disable-oc",  false, 0x18)
            ];
    }

    private static void Socket_FF3()
    {
        MP1_CMD = 0x3B10528;
        MP1_RSP = 0x3B10578;
        MP1_ARG = 0x3B10998;

        RSMU_CMD = 0x3B10a20;
        RSMU_RSP = 0x3B10a80;
        RSMU_ARG = 0x3B10a88;

        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x7),
                ("stapm-limit",true, 0x14),
                ("stapm-limit",false , 0x31), // Use RSMU address
                ("stapm-time",true , 0x18),
                ("fast-limit",true , 0x15),
                ("slow-limit",true , 0x16),
                ("slow-time",true , 0x17),
                ("tctl-temp",true , 0x19),
                ("cHTC-temp",false , 0x37),
                ("apu-skin-temp",true , 0x33),
                ("vrm-current",true , 0x1a),
                ("vrmmax-current",true , 0x1e),
                ("vrmsoc-current",true , 0x1b),
                ("vrmsocmax-current",true , 0x1d),
                ("vrmcvip-current",true , 0x1d),
                ("vrmgfx-current",true , 0x1c),
                ("vrmgfxmax-current",true , 0x1f),
                ("prochot-deassertion-ramp",true , 0x22),
                ("psi3cpu_current",true , 0x20),
                ("psi3gfx_current",true , 0x21),
                ("gfx-clk",false , 0x89),
                ("power-saving",true , 0x12),
                ("max-performance",true , 0x11),
                ("set-coall",true , 0x4c),
                ("set-coall",false , 0x5d),
                ("set-coper",true , 0x4b),
                ("set-cogfx",false , 0xb7)
            ];
    }

    private static void Socket_AM4_V1()
    {
        MP1_CMD = 0X3B10528;
        MP1_RSP = 0X3B10564;
        MP1_ARG = 0X3B10598;

        RSMU_CMD = 0x3B1051C;
        RSMU_RSP = 0X3B10568;
        RSMU_ARG = 0X3B10590;

        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x6),
                ("stapm-limit",false, 0x64), // Use RSMU address✓
                ("vrm-current",false , 0x65), //✓
                ("vrmmax-current",false , 0x66), //✓
                ("tctl-temp",false , 0x68), //✓
                ("pbo-scalar",false , 0x6a), //
                ("oc-clk", false, 0x6c), //
                ("per-core-oc-clk",false , 0x6d), //✕
                ("oc-volt", false, 0x6e), //
                ("enable-oc",true , 0x23), //
                ("enable-oc",false , 0x6b), //
                ("disable-oc",true , 0x24), //
            ];
    }

    private static void Socket_AM4_V2()
    {
        MP1_CMD = 0x3B10530;
        MP1_RSP = 0x3B1057C;
        MP1_ARG = 0x3B109C4;

        RSMU_CMD = 0x3B10524;
        RSMU_RSP = 0x3B10570;
        RSMU_ARG = 0x3B10A40;

        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x6),
                ("stapm-limit",true, 0x3D),
                ("stapm-limit",false, 0x53), // Use RSMU address
                ("vrm-current",true , 0x3B),
                ("vrm-current",false , 0x54),
                ("vrmmax-current",true , 0x3c),
                ("vrmmax-current",false , 0x55),
                ("tctl-temp",true , 0x23),
                ("tctl-temp",false , 0x56),
                ("pbo-scalar",false , 0x58),
                ("oc-clk", true, 0x26),
                ("oc-clk", false, 0x5c),
                ("per-core-oc-clk",true , 0x27),
                ("per-core-oc-clk",false , 0x5d),
                ("oc-volt", true, 0x28),
                ("oc-volt", false, 0x61),
                ("set-coall", true, 0x36),
                ("set-coall", false, 0xb),
                ("set-coper", true, 0x35),
                ("enable-oc",true , 0x24),
                ("enable-oc",false , 0x5a),
                ("disable-oc",true , 0x25),
                ("disable-oc",false , 0x5b),
            ];
    }

    private static void Socket_AM5_V1()
    {
        MP1_CMD = 0x3010508;
        MP1_RSP = 0x3010988;
        MP1_ARG = 0x3010984;

        RSMU_CMD = 0x3B10524;
        RSMU_RSP = 0x3B10570;
        RSMU_ARG = 0x3B10A40;

        Commands =
            [
                // Store the commands
                ("enable-feature",true , 0x5), // Use MP1 address
                ("disable-feature",true , 0x7),
                ("stapm-limit",true, 0x3e),
                ("stapm-limit",false, 0x56), // Use RSMU address
                ("vrm-current",true , 0x3c),
                ("vrm-current",false , 0x57),
                ("vrmmax-current",true , 0x3d),
                ("vrmmax-current",false , 0x58),
                ("tctl-temp",true , 0x3f),
                ("tctl-temp",false , 0x59),
                ("pbo-scalar",false , 0x5b),
                ("oc-clk", false, 0x5f),
                ("per-core-oc-clk",false , 0x60),
                ("oc-volt", false, 0x61),
                ("set-coall", false, 0x7),
                ("set-cogfx", false, 0x7),
                ("set-coper", false, 0x6),
                ("enable-oc",false , 0x5d),
                ("disable-oc",false , 0x5e),
            ];
    }
}
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.