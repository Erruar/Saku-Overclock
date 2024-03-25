namespace Saku_Overclock;
#pragma warning disable CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
#pragma warning disable CS0649 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
internal class Config
{
    public int Preset = 0;
    public bool bluetheme;
    public bool Infoupdate;
    public string fanvalue;
    public int fanconfig;
    public bool autofan;
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
    public bool fanex = false;
    public bool tempex = false;
    public string fan1v;
    public string fan2v;
}
#pragma warning restore CS8618 // Поле, не допускающее значения NULL, должно содержать значение, отличное от NULL, при выходе из конструктора. Возможно, стоит объявить поле как допускающее значения NULL.
