using System.Diagnostics;

namespace Saku_Overclock.Helpers;

/* This is refractored DriverHelper from ZenTimings (GPL-v3)
 * Its author is https://github.com/irusanov 
 * optimized to work with Saku Overclock by Sakurazhima Serzhik
 * there you can see the source files in detail https://github.com/irusanov/ZenTimings/blob/dev/WPF/DriverHelper.cs
 */

internal static class DriverHelper
{
    public static void InstallPawnIO()
    {
        var path = ExtractPawnIO();
        if (!string.IsNullOrEmpty(path))
        {
            var process = Process.Start(new ProcessStartInfo(path, "-install"));
            process?.WaitForExit();

            File.Delete(path);
        }
    }

    private static string? ExtractPawnIO()
    {
        var destination = Path.Combine(Directory.GetCurrentDirectory(), "PawnIO_setup.exe");

        try
        {
            var resourceStream = typeof(MainWindow).Assembly.GetManifestResourceStream("Saku_Overclock.Assets.PawnIO.PawnIO_setup.exe");
            using (resourceStream)
            using (var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write))
            {
                resourceStream?.CopyTo(fileStream);
            }

            return destination;
        }
        catch
        {
            return null;
        }
    }
}