namespace Saku_Overclock.JsonContainers.Helpers;
/*This is a config for QuickSMU commands file*/
public class QuickSmuCommands
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Symbol { get; set; } = "\uE8C8";
    public int MailIndex { get; set; }
    public string Command { get; set; } = "";
    public string Argument { get; set; } = "";
    public bool Startup { get; set; }
    public bool ApplyWith { get; set; } = true;
}