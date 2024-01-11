using System.Diagnostics;
using System.Windows.Threading;
using Microsoft.UI.Xaml.Controls;
using Newtonsoft.Json;
using Saku_Overclock.Helpers;
using Saku_Overclock.ViewModels;
#pragma warning disable IDE0059 // Ненужное присваивание значения
#pragma warning disable CS8601 // Возможно, назначение-ссылка, допускающее значение NULL.
namespace Saku_Overclock.Views;
public sealed partial class КулерPage : Page
{
    private Config config = new();
    public КулерViewModel ViewModel
    {
        get;
    }

    public КулерPage()
    {
        ViewModel = App.GetService<КулерViewModel>();
        InitializeComponent();
        ConfigLoad();
        FanInit();
        Update();
        GetTemp();
    }


    //JSON форматирование
    public void ConfigSave()
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "SakuOverclock"));
            File.WriteAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json", JsonConvert.SerializeObject(config));
        }
        catch
        {

        }
    }
    public void ConfigLoad()
    {
        try
        {
            config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\SakuOverclock\\config.json"));
        }
        catch
        {
            App.MainWindow.ShowMessageDialogAsync("Пресеты 3", "Критическая ошибка!");
        }
    }

    public void FanInit()
    {
        ConfigLoad();
        Selfan.SelectedIndex = config.fanconfig;
        if (config.fanenabled == true) { ConfigLoad(); Fan1.Value = config.fan1; Fan2.Value = config.fan2; Enabl.IsChecked = true; Readon.IsChecked = false; Disabl.IsChecked = false; Fan1Val.Text = Fan1.Value.ToString() + " %"; Fan2Val.Text = Fan2.Value.ToString() + " %"; if (Fan1.Value > 100) { Fan1Val.Text = "Auto"; }; if (Fan2.Value > 100) { Fan2Val.Text = "Auto"; }; };
        if (config.fanread == true) { Enabl.IsChecked = false; Readon.IsChecked = true; Disabl.IsChecked = false; };
        if (config.fandisabled == true) { Enabl.IsChecked = false; Readon.IsChecked = false; Disabl.IsChecked = true; };
        //better fan init
        if (Enabl.IsChecked == true)
        {
            NbfcFan1();
            if (Fan1.Value > 100)
            {
                Fan1Val.Text = "Auto";
                GetInfo0();
                Update();
                config.fan1 = 110.0;
                ConfigSave();
            }
            else
            {
                Fan1Pr.Value = Fan1.Value;
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                config.fanex = false;
                ConfigSave();
            }
            ConfigSave();
        }
        if (Readon.IsChecked == true)
        {
            Fan1Val.Text = "Auto";
            GetInfo0();
            Update();
        }
        FanInit2();
    }
    public void FanInit2()
    {
        ConfigLoad();
        switch (Selfan.SelectedIndex)
        {
            case 0:
                config.fanvalue = "Acer Aspire 1410";
                ConfigSave();
                break;
            case 1:
                config.fanvalue = "Acer Aspire 1810T";
                ConfigSave();
                break;
            case 2:
                config.fanvalue = "Acer Aspire 1810TZ";
                ConfigSave();
                break;
            case 3:
                config.fanvalue = "Acer Aspire 1825PTZ";
                ConfigSave();
                break;
            case 4:
                config.fanvalue = "Acer Aspire 5738G";
                ConfigSave();
                break;
            case 5:
                config.fanvalue = "Acer Aspire 5745G";
                ConfigSave();
                break;
            case 6:
                config.fanvalue = "Acer Aspire 5749";
                ConfigSave();
                break;
            case 7:
                config.fanvalue = "Acer Aspire 5930";
                ConfigSave();
                break;
            case 8:
                config.fanvalue = "Acer Aspire 7551G";
                ConfigSave();
                break;
            case 9:
                config.fanvalue = "Acer Aspire 7735";
                ConfigSave();
                break;
            case 10:
                config.fanvalue = "Acer Aspire 7740G";
                ConfigSave();
                break;
            case 11:
                config.fanvalue = "Acer Aspire 7741G";
                ConfigSave();
                break;
            case 12:
                config.fanvalue = "Acer Aspire E1-522";
                ConfigSave();
                break;
            case 13:
                config.fanvalue = "Acer Aspire E1-772";
                ConfigSave();
                break;
            case 14:
                config.fanvalue = "Acer Aspire E5-475G";
                ConfigSave();
                break;
            case 15:
                config.fanvalue = "Acer Aspire E5-575G";
                ConfigSave();
                break;
            case 16:
                config.fanvalue = "Acer Aspire E5-731";
                ConfigSave();
                break;
            case 17:
                config.fanvalue = "Acer Aspire LT-10Q";
                ConfigSave();
                break;
            case 18:
                config.fanvalue = "Acer Aspire One AO531h";
                ConfigSave();
                break;
            case 19:
                config.fanvalue = "Acer Aspire One AO751h";
                ConfigSave();
                break;
            case 20:
                config.fanvalue = "Acer Aspire One AOA110";
                ConfigSave();
                break;
            case 21:
                config.fanvalue = "Acer Aspire One AOA150";
                ConfigSave();
                break;
            case 22:
                config.fanvalue = "Acer Aspire S3";
                ConfigSave();
                break;
            case 23:
                config.fanvalue = "Acer Aspire S7-191";
                ConfigSave();
                break;
            case 24:
                config.fanvalue = "Acer Aspire S7-391";
                ConfigSave();
                break;
            case 25:
                config.fanvalue = "Acer Aspire V13";
                ConfigSave();
                break;
            case 26:
                config.fanvalue = "Acer Aspire V3-371";
                ConfigSave();
                break;
            case 27:
                config.fanvalue = "Acer Aspire V3-571G";
                ConfigSave();
                break;
            case 28:
                config.fanvalue = "Acer Aspire V5-551";
                ConfigSave();
                break;
            case 29:
                config.fanvalue = "Acer Aspire V5-572G";
                ConfigSave();
                break;
            case 30:
                config.fanvalue = "Acer Aspire VN7-572G V15 Nitro BE";
                ConfigSave();
                break;
            case 31:
                config.fanvalue = "Acer Aspire VN7-591G V15 Nitro BE";
                ConfigSave();
                break;
            case 32:
                config.fanvalue = "Acer Aspire VN7-791G V17 Nitro BE";
                ConfigSave();
                break;
            case 33:
                config.fanvalue = "Acer Aspire VN7-792G V17 Nitro BE";
                ConfigSave();
                break;
            case 34:
                config.fanvalue = "Acer Aspire VN7-793G V17 Nitro BE";
                ConfigSave();
                break;
            case 35:
                config.fanvalue = "Acer Extensa 5220";
                ConfigSave();
                break;
            case 36:
                config.fanvalue = "Acer Extensa 5630Z";
                ConfigSave();
                break;
            case 37:
                config.fanvalue = "Acer LT-10Q";
                ConfigSave();
                break;
            case 38:
                config.fanvalue = "Acer Predator G3-572";
                ConfigSave();
                break;
            case 39:
                config.fanvalue = "Acer TravelMate 7730G";
                ConfigSave();
                break;
            case 40:
                config.fanvalue = "Asus F5SR";
                ConfigSave();
                break;
            case 41:
                config.fanvalue = "Asus G53SX";
                ConfigSave();
                break;
            case 42:
                config.fanvalue = "Asus K43SD";
                ConfigSave();
                break;
            case 43:
                config.fanvalue = "Asus K501LX";
                ConfigSave();
                break;
            case 44:
                config.fanvalue = "Asus K501UX";
                ConfigSave();
                break;
            case 45:
                config.fanvalue = "Asus M52VA";
                ConfigSave();
                break;
            case 46:
                config.fanvalue = "Asus N550JV";
                ConfigSave();
                break;
            case 47:
                config.fanvalue = "Asus N551JB";
                ConfigSave();
                break;
            case 48:
                config.fanvalue = "Asus N56JR";
                ConfigSave();
                break;
            case 49:
                config.fanvalue = "Asus N56VZ";
                ConfigSave();
                break;
            case 50:
                config.fanvalue = "Asus ROG G501JW";
                ConfigSave();
                break;
            case 51:
                config.fanvalue = "Asus ROG G751JL";
                ConfigSave();
                break;
            case 52:
                config.fanvalue = "Asus ROG G751JY";
                ConfigSave();
                break;
            case 53:
                config.fanvalue = "Asus ROG G752VS";
                ConfigSave();
                break;
            case 54:
                config.fanvalue = "Asus ROG G752VT";
                ConfigSave();
                break;
            case 55:
                config.fanvalue = "Asus ROG G752VY";
                ConfigSave();
                break;
            case 56:
                config.fanvalue = "Asus ROG G75VX";
                ConfigSave();
                break;
            case 57:
                config.fanvalue = "Asus ROG GL702VM";
                ConfigSave();
                break;
            case 58:
                config.fanvalue = "Asus ROG GL702ZC";
                ConfigSave();
                break;
            case 59:
                config.fanvalue = "Asus Transformer 3 Pro";
                ConfigSave();
                break;
            case 60:
                config.fanvalue = "Asus Vivobook S400CA";
                ConfigSave();
                break;
            case 61:
                config.fanvalue = "Asus Vivobook TP301UA";
                ConfigSave();
                break;
            case 62:
                config.fanvalue = "ASUS Vivobook X580VD";
                ConfigSave();
                break;
            case 63:
                config.fanvalue = "Asus X301A1";
                ConfigSave();
                break;
            case 64:
                config.fanvalue = "Asus X540LA";
                ConfigSave();
                break;
            case 65:
                config.fanvalue = "Asus Zenbook Flip UX360UAK";
                ConfigSave();
                break;
            case 66:
                config.fanvalue = "Asus Zenbook Pro UX550VE";
                ConfigSave();
                break;
            case 67:
                config.fanvalue = "Asus Zenbook UX21E";
                ConfigSave();
                break;
            case 68:
                config.fanvalue = "Asus Zenbook UX301LA";
                ConfigSave();
                break;
            case 69:
                config.fanvalue = "Asus Zenbook UX302LA";
                ConfigSave();
                break;
            case 70:
                config.fanvalue = "Asus Zenbook UX310UA";
                ConfigSave();
                break;
            case 71:
                config.fanvalue = "Asus Zenbook UX310UAK";
                ConfigSave();
                break;
            case 72:
                config.fanvalue = "Asus Zenbook UX31A";
                ConfigSave();
                break;
            case 73:
                config.fanvalue = "Asus Zenbook UX32A";
                ConfigSave();
                break;
            case 74:
                config.fanvalue = "Asus Zenbook UX32LN";
                ConfigSave();
                break;
            case 75:
                config.fanvalue = "Asus Zenbook UX32VD";
                ConfigSave();
                break;
            case 76:
                config.fanvalue = "Asus Zenbook UX410UQ";
                ConfigSave();
                break;
            case 77:
                config.fanvalue = "Asus Zenbook UX430UA";
                ConfigSave();
                break;
            case 78:
                config.fanvalue = "Asus Zenbook UX430UQ";
                ConfigSave();
                break;
            case 79:
                config.fanvalue = "Asus Zenbook UX51VZA";
                ConfigSave();
                break;
            case 80:
                config.fanvalue = "Asus Zenbook UX530U";
                ConfigSave();
                break;
            case 81:
                config.fanvalue = "Dell Inspiron 7348";
                ConfigSave();
                break;
            case 82:
                config.fanvalue = "Dell Inspiron 7375";
                ConfigSave();
                break;
            case 83:
                config.fanvalue = "Dell Vostro 3350";
                ConfigSave();
                break;
            case 84:
                config.fanvalue = "Dell XPS M1530";
                ConfigSave();
                break;
            case 85:
                config.fanvalue = "Fujitsu ESPRIMO Mobile V5505";
                ConfigSave();
                break;
            case 86:
                config.fanvalue = "Gateway AOA110";
                ConfigSave();
                break;
            case 87:
                config.fanvalue = "Gateway AOA150";
                ConfigSave();
                break;
            case 88:
                config.fanvalue = "Gateway LT31";
                ConfigSave();
                break;
            case 89:
                config.fanvalue = "Gigabyte p35w v3";
                ConfigSave();
                break;
            case 90:
                config.fanvalue = "HP Compaq 15-s103tx";
                ConfigSave();
                break;
            case 91:
                config.fanvalue = "HP Compaq 615";
                ConfigSave();
                break;
            case 92:
                config.fanvalue = "HP Compaq 625";
                ConfigSave();
                break;
            case 93:
                config.fanvalue = "HP Compaq 6735s Turion X2 RM-72";
                ConfigSave();
                break;
            case 94:
                config.fanvalue = "HP Compaq 8710p";
                ConfigSave();
                break;
            case 95:
                config.fanvalue = "HP Compaq nw9440";
                ConfigSave();
                break;
            case 96:
                config.fanvalue = "HP Compaq Presario CQ40 Turion X2 RM-74";
                ConfigSave();
                break;
            case 97:
                config.fanvalue = "HP EliteBook 2560p";
                ConfigSave();
                break;
            case 98:
                config.fanvalue = "HP EliteBook 2570p";
                ConfigSave();
                break;
            case 99:
                config.fanvalue = "HP EliteBook 2760p";
                ConfigSave();
                break;
            case 100:
                config.fanvalue = "HP EliteBook 8560p";
                ConfigSave();
                break;
            case 101:
                config.fanvalue = "HP EliteBook 8560w";
                ConfigSave();
                break;
            case 102:
                config.fanvalue = "HP EliteBook 8760w";
                ConfigSave();
                break;
            case 103:
                config.fanvalue = "HP EliteBook Folio 1040 G1";
                ConfigSave();
                break;
            case 104:
                config.fanvalue = "HP EliteBook Folio 9470m";
                ConfigSave();
                break;
            case 105:
                config.fanvalue = "HP EliteBook Folio 9470m_i5-3427u_bios-F.66";
                ConfigSave();
                break;
            case 106:
                config.fanvalue = "HP ENVY m6 1206dx";
                ConfigSave();
                break;
            case 107:
                config.fanvalue = "HP ENVY m6 Sleekbook";
                ConfigSave();
                break;
            case 108:
                config.fanvalue = "HP ENVY m6-1254eo";
                ConfigSave();
                break;
            case 109:
                config.fanvalue = "HP ENVY x360 Convertible 13-ag0xxx";
                ConfigSave();
                break;
            case 110:
                config.fanvalue = "HP ENVY x360 Convertible 15-cn0xxx";
                ConfigSave();
                break;
            case 111:
                config.fanvalue = "HP Laptop 14-cm0xxx";
                ConfigSave();
                break;
            case 112:
                config.fanvalue = "HP OMEN Notebook PC 15";
                ConfigSave();
                break;
            case 113:
                config.fanvalue = "HP Pavilion 14-v066br";
                ConfigSave();
                break;
            case 114:
                config.fanvalue = "HP Pavilion 17-ab240nd";
                ConfigSave();
                break;
            case 115:
                config.fanvalue = "HP Pavilion dv6 6190";
                ConfigSave();
                break;
            case 116:
                config.fanvalue = "HP Pavilion dv6";
                ConfigSave();
                break;
            case 117:
                config.fanvalue = "HP Pavilion HDX18";
                ConfigSave();
                break;
            case 118:
                config.fanvalue = "HP ProBook 430 G1";
                ConfigSave();
                break;
            case 119:
                config.fanvalue = "HP ProBook 440 G3";
                ConfigSave();
                break;
            case 120:
                config.fanvalue = "HP ProBook 450 G1";
                ConfigSave();
                break;
            case 121:
                config.fanvalue = "HP ProBook 4520s";
                ConfigSave();
                break;
            case 122:
                config.fanvalue = "HP ProBook 4530s";
                ConfigSave();
                break;
            case 123:
                config.fanvalue = "HP ProBook 4535s";
                ConfigSave();
                break;
            case 124:
                config.fanvalue = "HP ProBook 4540s";
                ConfigSave();
                break;
            case 125:
                config.fanvalue = "HP ProBook 4710s";
                ConfigSave();
                break;
            case 126:
                config.fanvalue = "HP ProBook 4720s";
                ConfigSave();
                break;
            case 127:
                config.fanvalue = "HP ProBook 5330m";
                ConfigSave();
                break;
            case 128:
                config.fanvalue = "HP ProBook 6455b";
                ConfigSave();
                break;
            case 129:
                config.fanvalue = "HP ProBook 6460b";
                ConfigSave();
                break;
            case 130:
                config.fanvalue = "HP ProBook 6465b";
                ConfigSave();
                break;
            case 131:
                config.fanvalue = "HP ProBook 650 G1";
                ConfigSave();
                break;
            case 132:
                config.fanvalue = "HP ProBook 650 G2";
                ConfigSave();
                break;
            case 133:
                config.fanvalue = "HP ProBook 6550b";
                ConfigSave();
                break;
            case 134:
                config.fanvalue = "HP ProBook 6560b";
                ConfigSave();
                break;
            case 135:
                config.fanvalue = "HP Spectre x360 Convertible 13-ae0xx";
                ConfigSave();
                break;
            case 136:
                config.fanvalue = "HP ZBook 15";
                ConfigSave();
                break;
            case 137:
                config.fanvalue = "HP ZBook Studio G3";
                ConfigSave();
                break;
            case 138:
                config.fanvalue = "Lenovo Ideapad 500S-13ISK";
                ConfigSave();
                break;
            case 139:
                config.fanvalue = "Lenovo Ideapad 500S-14ISK";
                ConfigSave();
                break;
            case 140:
                config.fanvalue = "Lenovo Ideapad 510s";
                ConfigSave();
                break;
            case 141:
                config.fanvalue = "Lenovo Ideapad 710S";
                ConfigSave();
                break;
            case 142:
                config.fanvalue = "Lenovo Ideapad U160";
                ConfigSave();
                break;
            case 143:
                config.fanvalue = "Lenovo Ideapad U330p";
                ConfigSave();
                break;
            case 144:
                config.fanvalue = "Lenovo Ideapad U430p";
                ConfigSave();
                break;
            case 145:
                config.fanvalue = "Lenovo IdeaPad Y580";
                ConfigSave();
                break;
            case 146:
                config.fanvalue = "Lenovo ThinkPad 13";
                ConfigSave();
                break;
            case 147:
                config.fanvalue = "Lenovo ThinkPad Edge E520";
                ConfigSave();
                break;
            case 148:
                config.fanvalue = "Lenovo ThinkPad Helix";
                ConfigSave();
                break;
            case 149:
                config.fanvalue = "Lenovo ThinkPad T430s";
                ConfigSave();
                break;
            case 150:
                config.fanvalue = "Lenovo ThinkPad T440s";
                ConfigSave();
                break;
            case 151:
                config.fanvalue = "Lenovo ThinkPad T540p";
                ConfigSave();
                break;
            case 152:
                config.fanvalue = "Lenovo ThinkPad x121e";
                ConfigSave();
                break;
            case 153:
                config.fanvalue = "Lenovo ThinkPad x220i";
                ConfigSave();
                break;
            case 154:
                config.fanvalue = "Lenovo ThinkPad x230";
                ConfigSave();
                break;
            case 155:
                config.fanvalue = "Lenovo U31-70";
                ConfigSave();
                break;
            case 156:
                config.fanvalue = "Lenovo U41-70";
                ConfigSave();
                break;
            case 157:
                config.fanvalue = "Lenovo V580";
                ConfigSave();
                break;
            case 158:
                config.fanvalue = "Lenovo Yoga 11s";
                ConfigSave();
                break;
            case 159:
                config.fanvalue = "Lenovo Yoga 13 2191";
                ConfigSave();
                break;
            case 160:
                config.fanvalue = "Lenovo Yoga 2 13";
                ConfigSave();
                break;
            case 161:
                config.fanvalue = "Lenovo Yoga 3 14";
                ConfigSave();
                break;
            case 162:
                config.fanvalue = "Lenovo Yoga 510";
                ConfigSave();
                break;
            case 163:
                config.fanvalue = "Lenovo Yoga 710";
                ConfigSave();
                break;
            case 164:
                config.fanvalue = "Medion Akoya P6612";
                ConfigSave();
                break;
            case 165:
                config.fanvalue = "Medion Akoya P6630";
                ConfigSave();
                break;
            case 166:
                config.fanvalue = "Packard Bell AOA110";
                ConfigSave();
                break;
            case 167:
                config.fanvalue = "Packard Bell AOA150";
                ConfigSave();
                break;
            case 168:
                config.fanvalue = "Packard Bell DOA150";
                ConfigSave();
                break;
            case 169:
                config.fanvalue = "Packard Bell DOTMA";
                ConfigSave();
                break;
            case 170:
                config.fanvalue = "Packard Bell DOTMU";
                ConfigSave();
                break;
            case 171:
                config.fanvalue = "Packard Bell DOTVR46";
                ConfigSave();
                break;
            case 172:
                config.fanvalue = "Packard Bell Easynote TJ65";
                ConfigSave();
                break;
            case 173:
                config.fanvalue = "Packard Bell ENBFT";
                ConfigSave();
                break;
            case 174:
                config.fanvalue = "Sony Vaio SVE1711";
                ConfigSave();
                break;
            case 175:
                config.fanvalue = "Sony Vaio SVE1713Y1E";
                ConfigSave();
                break;
            case 176:
                config.fanvalue = "Sony Vaio SVF13N190X";
                ConfigSave();
                break;
            case 177:
                config.fanvalue = "Sony Vaio SVF14N1C5E";
                ConfigSave();
                break;
            case 178:
                config.fanvalue = "Sony Vaio SVT1312M1ES";
                ConfigSave();
                break;
            case 179:
                config.fanvalue = "Sony Vaio VPCF12S1E";
                ConfigSave();
                break;
            case 180:
                config.fanvalue = "Toshiba Satellite L740";
                ConfigSave();
                break;
            case 181:
                config.fanvalue = "Toshiba Satellite L745";
                ConfigSave();
                break;
            case 182:
                config.fanvalue = "Xiaomi Mi Book (TM1613, TM1703)";
                ConfigSave();
                break;

        }
    }
    private void Disabl_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fandisabled = true; config.fanread = false; config.fanenabled = false; config.fanex = false;
        ConfigSave();
        NbfcEnable();
    }

    private void Readon_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fanread = true; config.fanenabled = false; config.fandisabled = false;
        ConfigSave();
        NbfcEnable();
        Fan1Val.Text = "Auto";
        GetInfo0();
        Update();
    }

    private void Enabl_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        config.fanenabled = true; config.fandisabled = false; config.fanread = false;
        ConfigSave();
        NbfcEnable();
    }

    private async void Fan1_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Enabl.IsChecked == true)
        {
            NbfcFan1();
            await Task.Delay(200);
            config.fan1 = Fan1.Value;
            Fan1Val.Text = Fan1.Value.ToString() + " %";
            if (Fan1.Value > 100)
            {
                Fan1Val.Text = "Auto";
                GetInfo0();
                Update();
                config.fan1 = 110.0;
                ConfigSave();
            }
            else
            {
                Fan1Pr.Value = Fan1.Value;
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                config.fanex = false;
                ConfigSave();
            }
            ConfigSave();
        }
        if (Readon.IsChecked == true)
        {
            Fan1Val.Text = "Auto";
            GetInfo0();
            Update();
        }
        FanInit2();
    }

    private async void Fan2_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Enabl.IsChecked == true)
        {
            NbfcFan2();
            await Task.Delay(200);
            config.fan2 = Fan2.Value;
            Fan2Val.Text = Fan2.Value.ToString() + " %";
            if (Fan2.Value > 100)
            {
                Fan2Val.Text = "Auto";
                GetInfo0();
                Update();
                config.fan2 = 110.0;
                ConfigSave();
                if (Fan1Pr.Value == 10) { Fan1Pr.Value = 100; }
            }
            else
            {
                Fan2Pr.Value = Fan2.Value;
                Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value;
                config.fanex = false;
                ConfigSave();
            }
            ConfigSave();
        }
        if (Readon.IsChecked == true)
        {
            Fan2Val.Text = "Auto";
            GetInfo0();
            Update();
        }
    }

    public void NbfcEnable()
    {
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        if (config.fandisabled == true)
        {
            p.StartInfo.Arguments = " stop";
        }
        if (config.fanenabled == true)
        {
            p.StartInfo.Arguments = " start --enabled";
        }
        if (config.fanread == true)
        {
            p.StartInfo.Arguments = " start --readonly";
        }
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
        //App.MainWindow.ShowMessageDialogAsync("Вы успешно выставили свои настройки! \n" + mc.config.adjline, "Применение успешно!");
    }

    public void NbfcFan1()
    {
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        if (config.fanenabled == true)
        {
            if (Fan1.Value < 100) { p.StartInfo.Arguments = " set --fan 0 --speed " + Fan1.Value; }
            else { p.StartInfo.Arguments = " set --fan 0 --auto"; }
        }
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
    }
    public void NbfcFan2()
    {
        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        if (config.fanenabled == true)
        {
            if (Fan2.Value < 100) { p.StartInfo.Arguments = " set --fan 1 --speed " + Fan2.Value; }
            else { p.StartInfo.Arguments = " set --fan 1 --auto"; }
        }
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
    }
    public void NbfcFanState()
    {
        const string quote = "\"";

        Process p = new Process();
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.FileName = @"nbfc/nbfc.exe";
        ConfigLoad();
        p.StartInfo.Arguments = " config --apply " + quote + config.fanvalue + quote;
        p.StartInfo.CreateNoWindow = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();

        /*StreamReader outputWriter = p.StandardOutput;
        String errorReader = p.StandardError.ReadToEnd();
        String line = outputWriter.ReadLine();
        while (line != null)
        {

            if (line != "")
            {
                Ktext = Ktext + "\n" + line;
            }

            Ktext = outputWriter.ReadLine();
        }*/
        //App.MainWindow.ShowMessageDialogAsync("Вы успешно выставили свои настройки! \n" + " config --apply " + quote + config.fanvalue + quote, "Применение успешно!");
    }

    private async void Selfan_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await Task.Delay(200);
        config.fanconfig = Selfan.SelectedIndex;
        ConfigSave();
        FanInit2();
        NbfcFanState();

    }
    //set --fan 0 --speed 100
    //Информация о текущей скорости вращения кулеров
    public void GetInfo0()
    {
        ConfigLoad();
        DispatcherTimer timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(10000);
        ConfigLoad();
        if (config.fanex == false)
        {
            config.fanex = true;
            ConfigSave();
            timer.Tick += (sender, e) =>
            {
                // Запустите faninfo снова
                //MainWindow.Applyer.GetInfo();
                Process();
            };
            timer.Start();
        }
        else
        {
            timer.Stop();
            config.fanex = false;
            ConfigSave();
        }
    }
    public void Process()
    {
        ConfigLoad();
        if (config.fanex == true)
        {
            config.fan1v = "";
            config.fan2v = "";
            ConfigSave();
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"nbfc/nbfc.exe";
            p.StartInfo.Arguments = " status --fan 0";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.Start();
            StreamReader outputWriter = p.StandardOutput;
            var line = outputWriter.ReadLine();
            while (line != null)
            {

                if (line != "")
                {
                    config.fan1v += line;
                    ConfigSave();
                }

                line = outputWriter.ReadLine();
            }
            line = null;
            p.WaitForExit();

            //fan 2
            Process p1 = new Process();
            p1.StartInfo.UseShellExecute = false;
            p1.StartInfo.FileName = @"nbfc/nbfc.exe";
            p1.StartInfo.Arguments = " status --fan 1";
            p1.StartInfo.CreateNoWindow = true;
            p1.StartInfo.RedirectStandardError = true;
            p1.StartInfo.RedirectStandardInput = true;
            p1.StartInfo.RedirectStandardOutput = true;

            p1.Start();
            StreamReader outputWriter1 = p1.StandardOutput;
            var line1 = outputWriter1.ReadLine();
            while (line1 != null)
            {

                if (line1 != "")
                {
                    config.fan2v += line1;
                    ConfigSave();
                }

                line1 = outputWriter1.ReadLine();
            }
            line1 = null;
            p1.WaitForExit();

            //update an info
            if (config.fan1v != null)
            {
                Fan1Value.Text = config.fan1v;
                try
                {
                    Fan1Value.Text = Fan1Value.Text.Remove(0, 101);
                    Fan1Value.Text = Fan1Value.Text.Remove(3);
                    string Chara;
                    Chara = Fan1Value.Text;
                    try { Chara = Chara.Replace(".", ""); Fan1Value.Text = Chara; } catch { }
                    if (Convert.ToInt32(Fan1Value.Text) > 1)
                    {
                        Fan1Pr.Value = Convert.ToInt32(Fan1Value.Text);
                        if (Fan1Pr.Value == 10) { Fan1Pr.Value = 100; Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + " 100"; }
                    }
                    else { Fan1Pr.Value = 0; }
                    Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1Value.Text;
                    if (Fan1Pr.Value == 100) { Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + " 100"; }
                }
                catch
                {
                    if (Fan1.Value < 100)
                    {
                        Fan1Pr.Value = Fan1.Value;
                        Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                    }

                }
            }

            if (config.fan2v != null)
            {
                Fan2Value.Text = config.fan2v;
                try
                {
                    Fan2Value.Text = Fan2Value.Text.Remove(0, 101);
                    Fan2Value.Text = Fan2Value.Text.Remove(3);
                    string Chara1;
                    Chara1 = Fan2Value.Text;
                    try { Chara1 = Chara1.Replace(".", ""); Fan2Value.Text = Chara1; } catch { }
                    if (Convert.ToInt32(Fan2Value.Text) > 1)
                    {
                        Fan2Pr.Value = Convert.ToInt32(Fan2Value.Text);
                    }
                    else { Fan2Pr.Value = 0; }
                    Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2Value.Text;
                }
                catch
                {
                    if (Fan2.Value < 100)
                    {
                        Fan2Pr.Value = Fan2.Value;
                        Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value;
                    }

                }
            }
        }
    }
    public void GetTemp()
    {

        DispatcherTimer timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(100.0);
        ConfigLoad();
        config.tempex = true;
        ConfigSave();
        timer.Tick += (sender, e) =>
        {
            GetTemp1();
        };
        timer.Start();
    }
    public void GetTemp1()
    {
        if (config.tempex == true)
        {
            Temp.Text = "";
            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.FileName = @"ryzenadj.exe";
            p.StartInfo.Arguments = "-i";
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;

            p.Start();

            StreamReader outputWriter = p.StandardOutput;
            var line = outputWriter.ReadLine();
            while (line != null)
            {

                if (line != "")
                {
                    Temp.Text = Temp.Text + "\n" + line;
                }

                line = outputWriter.ReadLine();
            }

            p.WaitForExit();
#pragma warning disable IDE0059 // Ненужное присваивание значения
            line = null;
#pragma warning restore IDE0059 // Ненужное присваивание значения
            //App.MainWindow.ShowMessageDialogAsync("Вы успешно выставили свои настройки! \n" + mc.config.adjline, "Применение успешно!");
        }
    }
    private void Update()
    {
        ConfigLoad();
        if (config.fan1v != null)
        {
            Fan1Value.Text = config.fan1v;
            try
            {
                Fan1Value.Text = Fan1Value.Text.Remove(0, 101);
                Fan1Value.Text = Fan1Value.Text.Remove(3);
                string Chara;
                Chara = Fan1Value.Text;
                try { Chara = Chara.Replace(".", ""); Fan1Value.Text = Chara; } catch { }
                if (Convert.ToInt32(Fan1Value.Text) > 1)
                {
                    Fan1Pr.Value = Convert.ToInt32(Fan1Value.Text);
                    if (Fan1Pr.Value == 10) { Fan1Pr.Value = 100; }
                }
                else { Fan1Pr.Value = 0; }
                Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1Value.Text;
                if (Fan1Pr.Value == 100) { Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + " 100"; }
            }
            catch
            {
                if (Fan1.Value < 100)
                {
                    Fan1Pr.Value = Fan1.Value;
                    Fan1Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan1.Value;
                }
            }
        }

        if (config.fan2v != null)
        {
            Fan2Value.Text = config.fan2v;
            try
            {
                Fan2Value.Text = Fan2Value.Text.Remove(0, 101);
                Fan2Value.Text = Fan2Value.Text.Remove(3);
                string Chara1;
                Chara1 = Fan2Value.Text;
                try { Chara1 = Chara1.Replace(".", ""); Fan2Value.Text = Chara1; } catch { }
                if (Convert.ToInt32(Fan2Value.Text) > 1)
                {
                    Fan2Pr.Value = Convert.ToInt32(Fan2Value.Text);
                }
                else { Fan2Pr.Value = 0; }
                Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2Value.Text;
            }
            catch
            {
                if (Fan2.Value < 100)
                {
                    Fan2Pr.Value = Fan2.Value;
                    Fan2Cur.Text = "Cooler_Current_Fan_Val".GetLocalized() + "   " + Fan2.Value;
                }

            }
        }
    }

    private void Fan1Pr_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (Fan1Pr.Value == 10) { Fan1Pr.Value = 100; }
    }
}
