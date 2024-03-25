using System.Diagnostics;
using System.Management;
using static Saku_Overclock.SMUEngine.Family;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
internal static class FamilyHelpers
{

    public static RyzenFamily FAM = RyzenFamily.Unknown;

    public static ProcessorType TYPE = ProcessorType.Unknown;


    public static string CPUName = "";
    public static int CPUFamily = 0;
    public static int CPUModel = 0;
    public static int CPUStepping = 0;
    public static async void setCpuFamily()
    {
        try
        {
            var processorIdentifier = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");

            // Split the string into individual words
            var words = processorIdentifier.Split(' ');

            // Find the indices of the words "Family", "Model", and "Stepping"
            var familyIndex = Array.IndexOf(words, "Family") + 1;
            var modelIndex = Array.IndexOf(words, "Model") + 1;
            var steppingIndex = Array.IndexOf(words, "Stepping") + 1;

            // Extract the family, model, and stepping values from the corresponding words
            CPUFamily = int.Parse(words[familyIndex]);
            CPUModel = int.Parse(words[modelIndex]);
            CPUStepping = int.Parse(words[steppingIndex].TrimEnd(','));

            var mos = new ManagementObjectSearcher("root\\CIMV2", "SELECT * FROM Win32_Processor");
            foreach (var mo in mos.Get().Cast<ManagementObject>())
            {
                CPUName = mo["Name"].ToString();
            }
        }
        catch (ManagementException e)
        {
            Debug.WriteLine("Error: " + e.Message);
        }

        if (CPUName.Contains("Intel"))
        {
            TYPE = ProcessorType.Intel;
        }
        else
        {
            //Zen1 - Zen2
            if (CPUFamily == 23)
            {
                if (CPUModel == 1)
                {
                    FAM = RyzenFamily.SummitRidge;
                }

                if (CPUModel == 8)
                {
                    FAM = RyzenFamily.PinnacleRidge;
                }

                if (CPUModel == 17 || CPUModel == 18)
                {
                    FAM = RyzenFamily.RavenRidge;
                }

                if (CPUModel == 24)
                {
                    FAM = RyzenFamily.Picasso;
                }

                if (CPUModel == 32 && CPUName.Contains("15e") || CPUModel == 32 && CPUName.Contains("15Ce") || CPUModel == 32 && CPUName.Contains("20e"))
                {
                    FAM = RyzenFamily.Pollock;
                }
                else if (CPUModel == 32)
                {
                    FAM = RyzenFamily.Dali;
                }

                if (CPUModel == 80)
                {
                    FAM = RyzenFamily.FireFlight;
                }

                if (CPUModel == 96)
                {
                    FAM = RyzenFamily.Renoir;
                }

                if (CPUModel == 104)
                {
                    FAM = RyzenFamily.Lucienne;
                }

                if (CPUModel == 113)
                {
                    FAM = RyzenFamily.Matisse;
                }

                if (CPUModel == 144)
                {
                    FAM = RyzenFamily.VanGogh;
                }

                if (CPUModel == 160)
                {
                    FAM = RyzenFamily.Mendocino;
                }
            }

            //Zen3 - Zen4
            if (CPUFamily == 25)
            {
                if (CPUModel == 33)
                {
                    FAM = RyzenFamily.Vermeer;
                }

                if (CPUModel == 63 || CPUModel == 68)
                {
                    FAM = RyzenFamily.Rembrandt;
                }

                if (CPUModel == 80)
                {
                    FAM = RyzenFamily.Cezanne_Barcelo;
                }

                if (CPUModel == 97 && CPUName.Contains("HX"))
                {
                    FAM = RyzenFamily.DragonRange;
                }
                else if (CPUModel == 97)
                {
                    FAM = RyzenFamily.Raphael;
                }

                if (CPUModel == 116)
                {
                    FAM = RyzenFamily.PhoenixPoint;
                }

                if (CPUModel == 120)
                {
                    FAM = RyzenFamily.PhoenixPoint2;
                }
            }

            // Zen5 - Zen6
            if (CPUFamily == 26)
            {
                if (CPUModel == 32)
                {
                    FAM = RyzenFamily.StrixPoint;
                }
                else
                {
                    FAM = RyzenFamily.GraniteRidge;
                }
            }

            if (FAM == RyzenFamily.SummitRidge || FAM == RyzenFamily.PinnacleRidge || FAM == RyzenFamily.Matisse || FAM == RyzenFamily.Vermeer || FAM == RyzenFamily.Raphael || FAM == RyzenFamily.GraniteRidge)
            {
                TYPE = ProcessorType.Amd_Desktop_Cpu;
            }
            else
            {
                TYPE = ProcessorType.Amd_Apu;
            }
        }

        //Clipboard.SetText(System.Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER").ToString());
        //MessageBox.Show(CPUFamily.ToString() + " "  + FAM.ToString());
    }
}