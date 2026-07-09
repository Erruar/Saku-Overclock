using System.Diagnostics;

namespace Saku_Overclock.Helpers;

internal static class DriverHelper
{
    /// <summary>
    ///     Install PawnIO driver (Need rework)
    /// </summary>
    public static void InstallPawnIo()
    {
        var path = ExtractPawnIo();
        if (!string.IsNullOrEmpty(path))
        {
            var process = Process.Start(new ProcessStartInfo(path, "-install"));
            process?.WaitForExit();

            File.Delete(path);
        }
    }

    /// <summary>
    ///     Extract PawnIO from embedded resources
    /// </summary>
    /// <returns>Destination path</returns>
    private static string? ExtractPawnIo()
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