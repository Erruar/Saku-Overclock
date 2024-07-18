namespace Saku_Overclock;
#pragma warning disable CS8618 // ����, �� ����������� �������� NULL, ������ ��������� ��������, �������� �� NULL, ��� ������ �� ������������. ��������, ����� �������� ���� ��� ����������� �������� NULL.
#pragma warning disable CS0649 // ����, �� ����������� �������� NULL, ������ ��������� ��������, �������� �� NULL, ��� ������ �� ������������. ��������, ����� �������� ���� ��� ����������� �������� NULL.
internal class Config
{
    //��������� ����������
    public bool OldTitleBar = false; //���� ������� ����� ���� ����������
    public bool FixedTitleBar = false; //���� �������������� ����� ���� ����������
    public int AutostartType = 0; //��� ����������: 0 - ����, 1 - ��� ������� ���������� ����� � ����, 2 - ��������� � ��������, 3 - ��������� � ����
    public bool CheckForUpdates = true; //���� ��������������. ������� ����� ����������
    public bool ReapplyLatestSettingsOnAppLaunch = true; //��� ������� ���������� ������������� ��������� ���������� ��������� � ������� ��� ����� �������� ����������
    public bool ReapplySafeOverclock = true; //������������� ��������� ���������� ���������� ��������� ������
    public bool ReapplyOverclock = true; //������������� ��������� ���������� ��������� ������
    public double ReapplyOverclockTimer = 3.0; //������������� ��������� ���������� ��������� ������ (����� � ��������)
    public int ThemeType = 0; //��������� ����
    //�������, ������� ������� � ���������
    public int Preset = 0; //��������� ������������� ������
    public string RyzenADJline = ""; //���������� RyzenADJline ������� ��������� ��� ���������, �������� �� �������������� � �������
    public string ApplyInfo = ""; //���������� �� ������� ��������� ���������� ����������
    public bool RangeApplied = false; //������� �� �������� ��� ������� SMU
    public bool PremadeMinActivated = false; //������� �������
    public bool PremadeEcoActivated = false; //������� �������
    public bool PremadeBalanceActivated = false; //������� �������
    public bool PremadeSpeedActivated = false; //������� �������
    public bool PremadeMaxActivated = true; //������� �������
    public bool FlagRyzenADJConsoleRunning = false; //������ ���� ���� ����������� �����-�� �������� � ��������, �������� RyzenADJ
    //�������� ���������� �������
    public string NBFCConfigXMLName; //��� ���������� ����� ������� nbfc
    public bool NBFCAutoUpdateInformation = true; //������������� ��������� ��������� � ��������� �������
    public bool NBFCServiceStatusDisabled = true; //����� ������������� nbfc �� �������� ���������� �������
    public bool NBFCServiceStatusReadOnly = false; //����� ������������� nbfc �� �������� ���������� �������
    public bool NBFCServiceStatusEnabled = false; //����� ������������� nbfc �� �������� ���������� �������
    public double NBFCFan1UserFanSpeedRPM = 110.0; //������������ ������������� �������� �������� ������ �������, ������ 100 - ���� 
    public double NBFCFan2UserFanSpeedRPM = 110.0; //������������ ������������� �������� �������� ������ �������, ������ 100 - ���� 
    public bool NBFCFlagConsoleCheckSpeedRunning = false; //���� ��� �������� ���������� �������, ������ ���������� �������� � �������� ��� ������ nbfc
    public bool FlagRyzenADJConsoleTemperatureCheckRunning = false; //���� ��� �������� ���������� �������,  ������ ���������� �������� � �������� ��� ������ RyzenADJ �������� �����������
    public string NBFCAnswerSpeedFan1; //����� nbfc ������� ��� ������ ���������� �� ������� ������
    public string NBFCAnswerSpeedFan2; //����� nbfc ������� ��� ������ ���������� �� ������� ������
}
#pragma warning restore CS8618 // ����, �� ����������� �������� NULL, ������ ��������� ��������, �������� �� NULL, ��� ������ �� ������������. ��������, ����� �������� ���� ��� ����������� �������� NULL.
