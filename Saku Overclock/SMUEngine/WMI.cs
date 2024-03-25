using System.Management;
using static System.Management.ManagementObjectCollection;
/*This is a modified processor driver file. Zen-States.Core Version is 1.6.8.1. Its author is https://github.com/irusanov
This file has been refactored many times and optimized to work with Saku Overclock by Sakurazhima Serzhik. I do not recommend rereading this file, it is better to familiarize yourself with https://github.com/irusanov/ZenStates-Core
there you can see the source files in detail*/
namespace Saku_Overclock.SMUEngine;
public static class WMI
{
    public static object TryGetProperty(ManagementObject wmiObj, string propertyName)
    {
        object retval = null;
        try
        {
            retval = wmiObj.GetPropertyValue(propertyName);
        }
        catch (ManagementException ex)
        {
            Console.WriteLine(ex.Message);
        }

        return retval;
    }

    //root\wmi
    public static ManagementScope Connect(string scope)
    {
        try
        {

            var mScope = new ManagementScope($@"{scope}");
            mScope.Connect();

            return mScope.IsConnected ? mScope : throw new ManagementException($@"Failed to connect to {scope}");
        }
        catch (ManagementException ex)
        {
            Console.WriteLine(@"WMI: {0}", ex.Message);
            throw;
        }
    }

    public static ManagementObject Query(string scope, string wmiClass)
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher($"{scope}", $"SELECT * FROM {wmiClass}"))
            {
                var enumerator = searcher.Get().GetEnumerator();
                if (enumerator.MoveNext())
                    return enumerator.Current as ManagementObject;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return null;
    }

    public static List<string> GetWmiNamespaces(string root)
    {
        var namespaces = new List<string>();
        try
        {
            var nsClass =
                new ManagementClass(new ManagementScope(root), new ManagementPath("__namespace"), null);
            foreach (var obj in nsClass.GetInstances())
            {
                var ns = (ManagementObject)obj;
                var namespaceName = root + "\\" + ns["Name"];
                namespaces.Add(namespaceName);
                namespaces.AddRange(GetWmiNamespaces(namespaceName));
            }

            namespaces.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return namespaces;
    }

    public static List<string> GetClassNamesWithinWmiNamespace(string wmiNamespaceName)
    {
        var classNames = new List<string>();
        try
        {
            var searcher = new ManagementObjectSearcher
            (new ManagementScope(wmiNamespaceName),
                new WqlObjectQuery("SELECT * FROM meta_class"));
            var objectCollection = searcher.Get();
            foreach (var obj in objectCollection)
            {
                var wmiClass = (ManagementClass)obj;
                var stringified = wmiClass.ToString();
                var parts = stringified.Split(':');
                classNames.Add(parts[1]);
            }

            classNames.Sort(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }

        return classNames;
    }

    public static string GetInstanceName(string scope, string wmiClass)
    {
        using (var queryObject = Query(scope, wmiClass))
        {
            var name = "";

            if (queryObject == null)
                return name;

            try
            {
                var obj = TryGetProperty(queryObject, "InstanceName");
                if (obj != null) name = obj.ToString();
            }
            catch
            {
                // ignored
            }

            return name;
        }
    }

    public static ManagementBaseObject InvokeMethod(ManagementObject mo, string methodName, string propName,
        string inParamName, uint arg)
    {
        try
        {
            // Obtain in-parameters for the method
            var inParams = mo.GetMethodParameters($"{methodName}");

            // Add the input parameters.
            if (inParams != null)
                inParams[$"{inParamName}"] = arg;

            // Execute the method and obtain the return values.
            var outParams = mo.InvokeMethod($"{methodName}", inParams, null);

            return (ManagementBaseObject)outParams?.Properties[$"{propName}"].Value;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }

    public static byte[] RunCommand(ManagementObject mo, uint commandID, uint commandArg = 0x0)
    {
        try
        {
            // Obtain in-parameters for the method
            var inParams = mo.GetMethodParameters("RunCommand");

            // Add the input parameters.
            var buffer = new byte[8];

            var cmd = BitConverter.GetBytes(commandID);
            var arg = BitConverter.GetBytes(commandArg);

            Buffer.BlockCopy(cmd, 0, buffer, 0, 4);
            Buffer.BlockCopy(arg, 0, buffer, 4, 4);

            inParams["Inbuf"] = buffer;

            // Execute the method and obtain the return values.
            var outParams = mo.InvokeMethod("RunCommand", inParams, null);

            // return outParam
            var pack = (ManagementBaseObject)outParams?.Properties["Outbuf"].Value;
            return (byte[])pack?.GetPropertyValue("Result");
        }
        catch (ManagementException ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }
}
