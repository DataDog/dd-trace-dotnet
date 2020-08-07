#if NETFRAMEWORK
using System;
using System.Linq;
using Datadog.Trace.Logging;
using Microsoft.Win32;

namespace Datadog.Trace
{
    internal partial class FrameworkDescription
    {
        public static FrameworkDescription Create()
        {
            // .NET Framework
            return new FrameworkDescription(
                ".NET Framework",
                GetNetFrameworkVersion() ?? "unknown",
                "Windows",
                Environment.Is64BitOperatingSystem ? "x64" : "x86",
                Environment.Is64BitProcess ? "x64" : "x86");
        }

        private static string GetNetFrameworkVersion()
        {
            string productVersion = null;

            try
            {
                object registryValue;

                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    registryValue = subKey?.GetValue("Release");
                }

                if (registryValue is int release)
                {
                    // find the known version on the list with the largest release number
                    // that is lower than or equal to the release number in the Windows Registry
                    productVersion = DotNetFrameworkVersionMapping.FirstOrDefault(t => release >= t.Item1)?.Item2;
                }
            }
            catch (Exception e)
            {
                Log.SafeLogError(e, "Error getting .NET Framework version from Windows Registry");
            }

            if (productVersion == null)
            {
                // if we fail to extract version from assembly path,
                // fall back to the [AssemblyInformationalVersion] or [AssemblyFileVersion]
                productVersion = GetVersionFromAssemblyAttributes();
            }

            if (productVersion == null)
            {
                // at this point, everything else has failed (this is probably the same as [AssemblyFileVersion] above)
                productVersion = Environment.Version.ToString();
            }

            return productVersion;
        }
    }
}
#endif
