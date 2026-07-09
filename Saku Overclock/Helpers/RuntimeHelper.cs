using System.Runtime.InteropServices;
using System.Text;

namespace Saku_Overclock.Helpers;

public abstract class RuntimeHelper
{
    /// <summary>
    ///     Get current package name
    /// </summary>
    /// <param name="packageFullNameLength">Name lenght</param>
    /// <param name="packageFullName">Name</param>
    /// <returns>Status</returns>
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(ref int packageFullNameLength, StringBuilder? packageFullName);

    /// <summary>
    ///     Get IsMsix bundle
    /// </summary>
    public static bool IsMsix
    {
        get
        {
            var length = 0;

            return GetCurrentPackageFullName(ref length, null) != 15700L;
        }
    }
}