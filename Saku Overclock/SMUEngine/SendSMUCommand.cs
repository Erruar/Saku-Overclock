using System.Diagnostics;
using Newtonsoft.Json;
using Saku_Overclock.Contracts.Services;
using Saku_Overclock.Helpers;
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.

namespace Saku_Overclock.SMUEngine;
internal class SendSMUCommand
{
    private readonly Cpu cpu;
    private Smusettings smusettings = new();
    private Config config = new();
    private readonly Mailbox testMailbox = new();
    private Devices devices = new();
    private readonly Profile[] profile = new Profile[1];
    public string? ocmode;

    [Obsolete]
    public SendSMUCommand()
    {
        try
        {
            cpu = new Cpu();
            Cpu.Cpu_Init();
        }
        catch
        {
            App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
        }
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
        catch
        {
            return true;
        }
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
        catch
        {
            JsonRepair('s');
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
        if (file == 'd')
        {
            try
            {
                for (var j = 0; j < 5; j++)
                {
                    devices = new Devices();
                }
            }
            catch
            {
                App.MainWindow.Close();
            }
            if (devices != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
                }
                catch
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
                    App.GetService<IAppNotificationService>().Show(string.Format("AppNotificationCrash".GetLocalized(), AppContext.BaseDirectory));
                    App.MainWindow.Close();
                }
            }
            else
            {
                try
                {
                    File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json");
                    Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
                    File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\devices.json", JsonConvert.SerializeObject(devices));
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
    [Obsolete("Obsolete")]
    public void Play_Invernate_QuickSMU(int mode)
    {
        SmuSettingsLoad();
        if (smusettings.QuickSMUCommands == null)
        {
            return;
        }

        for (var i = 0; i < smusettings.QuickSMUCommands.Count; i++)
        {
            if (mode == 0)
            {
                if (smusettings.QuickSMUCommands[i].ApplyWith || smusettings.QuickSMUCommands[i].Startup)
                {
                    ApplySettings(i);
                }
            }
            else
            {
                if (smusettings.QuickSMUCommands[i].Startup)
                {
                    ApplySettings(i);
                }
            }
        }
    }
    [Obsolete("Obsolete")]
    public void ApplySettings(int CommandIndex)
    {
        try
        {
            uint[]? args;
            string[]? userArgs;
            uint addrMsg;
            uint addrRsp;
            uint addrArg;
            uint command;
            SmuSettingsLoad();
            args = Utils.MakeCmdArgs();
            userArgs = smusettings.QuickSMUCommands[CommandIndex].Argument.Trim().Split(',');
            TryConvertToUint(smusettings.MailBoxes[smusettings.QuickSMUCommands[CommandIndex].MailIndex].CMD, out addrMsg);
            TryConvertToUint(smusettings.MailBoxes[smusettings.QuickSMUCommands[CommandIndex].MailIndex].RSP, out addrRsp);
            TryConvertToUint(smusettings.MailBoxes[smusettings.QuickSMUCommands[CommandIndex].MailIndex].ARG, out addrArg);
            TryConvertToUint(smusettings.QuickSMUCommands[CommandIndex].Command, out command);
            testMailbox.SMU_ADDR_MSG = addrMsg;
            testMailbox.SMU_ADDR_RSP = addrRsp;
            testMailbox.SMU_ADDR_ARG = addrArg;
            for (var i = 0; i < userArgs.Length; i++)
            {
                if (i == args.Length)
                {
                    break;
                }
                TryConvertToUint(userArgs[i], out var temp);
                args[i] = temp;
            }
            var status = cpu.Smu.SendSmuCommand(testMailbox, command, ref args);
        }
        catch
        {

        }
    }
}
#pragma warning restore CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
