namespace Saku_Overclock;
#pragma warning disable CS8618 // ����, �� ����������� �������� NULL, ������ ��������� ��������, �������� �� NULL, ��� ������ �� ������������. ��������, ����� �������� ���� ��� ����������� �������� NULL.
#pragma warning disable CS0649 // ����, �� ����������� �������� NULL, ������ ��������� ��������, �������� �� NULL, ��� ������ �� ������������. ��������, ����� �������� ���� ��� ����������� �������� NULL.
internal class Config
{
    public bool OldTitleBar = false;
    public bool FixedTitleBar = false;
    public string ApplyInfo;
    public int Preset = 0;
    public bool RangeApplied = true; 
    public int ThemeType = 0; 
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
    public int AutostartType = 0; 
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
#pragma warning restore CS8618 // ����, �� ����������� �������� NULL, ������ ��������� ��������, �������� �� NULL, ��� ������ �� ������������. ��������, ����� �������� ���� ��� ����������� �������� NULL.
