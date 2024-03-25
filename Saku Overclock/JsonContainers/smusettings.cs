using Saku_Overclock.SMUEngine;

namespace Saku_Overclock;

public class Smusettings
{
    public string Note = ""; 
    public  List<CustomMailBoxes> MailBoxes
    {
        get; set;
    }
    public  List<QuickSMUCommands> QuickSMUCommands
    {
        get; set;
    } 
}
