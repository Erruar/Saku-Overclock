using Saku_Overclock.SMUEngine;

namespace Saku_Overclock.JsonContainers;

public class Smusettings
{
    public string Note = "";

    public List<CustomMailBoxes>? MailBoxes
    {
        get;
        set;
    }

    public List<QuickSmuCommands>? QuickSmuCommands
    {
        get;
        set;
    }
}
