namespace Saku_Overclock.SMUEngine;
/*This is a config for QuickSMU commands file*/
public class QuickSMUCommands
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Symbol { get; set; } = "\uE8C8";
    public int MailIndex { get; set; } = 0;
    public string Command { get; set; } = "";
    public string Argument { get; set; } = "";
    public bool Startup { get; set; } = false;
    public bool ApplyWith { get; set; } = true;
}