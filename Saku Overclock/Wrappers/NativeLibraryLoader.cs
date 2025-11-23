using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Saku_Overclock.Wrappers;

public static partial class NativeLibraryLoader
{
    // 1. Define the P/Invoke signatures for the native Windows API functions
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern FreeLibrarySafeHandle LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr hLibModule);

    // Note: GetProcAddress only comes in an ANSI flavor (accepts non-Unicode strings), 
    // so CharSet.Ansi and ExactSpelling = true are commonly used.
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

    // 2. Define the SafeHandle class used by LoadLibrary
    // The SafeHandle ensures FreeLibrary is called automatically
    public sealed partial class FreeLibrarySafeHandle() : SafeHandleZeroOrMinusOneIsInvalid(true)
    {
        protected override bool ReleaseHandle() => FreeLibrary(handle);
    }

    // 3. The public method that replicates the original line's logic
    public static FreeLibrarySafeHandle LoadArchitectureSpecificLibrary(string dllName, string dllName64)
    {
        var libraryPath = Environment.Is64BitProcess ? dllName64 : dllName;

        // This is the functional equivalent of your original line of code:
        var module = LoadLibrary(libraryPath);

        if (module.IsInvalid)
        {
            // You should add error handling here (e.g., throwing an exception)
            // based on Marshal.GetLastWin32Error()
            throw new Win32Exception(Marshal.GetLastWin32Error(), $"Failed to load library: {libraryPath}");
        }

        return module;
    }

    /// <summary>
    ///     Retrieves the address of an exported function from the loaded DLL module.
    /// </summary>
    /// <param name="handle">The safe handle to the DLL module.</param>
    /// <param name="functionName">The name of the function to retrieve.</param>
    /// <returns>An IntPtr representing the unmanaged function pointer.</returns>
    public static IntPtr GetProcAddress(FreeLibrarySafeHandle handle, string functionName)
    {
        // Use the dangerous GetProcAddress method from our P/Invoke declaration, 
        // passing the underlying raw IntPtr from the SafeHandle
        var functionPointer = GetProcAddress(handle.DangerousGetHandle(), functionName);

        if (functionPointer == IntPtr.Zero)
        {
            // The function was not found or an error occurred
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Failed to find function: {functionName} in the loaded library.");
        }

        return functionPointer;
    }
}