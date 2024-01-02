using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Perception.Spatial;

namespace Saku_Overclock;

internal class Config
{
    public bool Infoupdate;
    public string fanvalue;
    public int fanconfig;
    public bool fandisabled;
    public bool fanread;
    public bool fanenabled = true;
    public double fan1;
    public double fan2;
    public bool reapplyinfo;
    public bool autostart;
    public bool traystart;
    public bool autooverclock;
    public bool reapplytime;
    public double reapplytimer;
    public bool autoupdates;
    public string adjline;
    public bool Min = false;
    public bool Eco = false;
    public bool Balance = false;
    public bool Speed = false;
    public bool Max = true;
    public bool execute = false;
}
