namespace Saku_Overclock.Models;

/// <summary>
///     Расширенный конфиг для команд QuickSMU
/// </summary>
public class QuickSmuCommands
{
    public string Name
    {
        get;
        set;
    } = "";

    public string Description
    {
        get;
        set;
    } = "";

    public string Symbol
    {
        get;
        set;
    } = "\uE8C8";

    public int MailIndex
    {
        get;
        set;
    }

    public string Command
    {
        get;
        set;
    } = "";

    public string Argument
    {
        get;
        set;
    } = "";

    public bool Startup
    {
        get;
        set;
    }

    public bool ApplyWith
    {
        get;
        set;
    } = true;
}