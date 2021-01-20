#if !NETFRAMEWORK
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal partial class FrameworkDescription
    {
        private static FrameworkDescription _instance = null;

        public static FrameworkDescription Instance
        {
            get { return _instance ?? (_instance = Create()); }
        }

        public static FrameworkDescription Create()
        {
            var frameworkName = "unknown";
            var frameworkVersion = "unknown";
            var osPlatform = "unknown";
            var osArchitecture = "unknown";
            var processArchitecture = "unknown";

            try
            {
                try
                {
                    // RuntimeInformation.FrameworkDescription returns a string like ".NET Framework 4.7.2" or ".NET Core 2.1",
                    // we want to return everything before the last space
                    frameworkVersion = RuntimeInformation.FrameworkDescription;
                    int index = frameworkVersion.LastIndexOf(' ');
                    frameworkName = frameworkVersion.Substring(0, index).Trim();
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error getting framework name from RuntimeInformation");
                }

                if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    osPlatform = "Windows";
                }
                else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    osPlatform = "Linux";
                }
                else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    osPlatform = "MacOS";
                }

                osArchitecture = RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
                processArchitecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
                frameworkVersion = GetNetCoreOrNetFrameworkVersion();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting framework description.");
            }

            return new FrameworkDescription(
                name: frameworkName,
                productVersion: frameworkVersion,
                osPlatform: osPlatform,
                osArchitecture: osArchitecture,
                processArchitecture: processArchitecture);
        }

        private static string GetNetCoreOrNetFrameworkVersion()
        {
            string productVersion = null;

            if (Environment.Version.Major == 3 || Environment.Version.Major >= 5)
            {
                // Environment.Version returns "4.x" in .NET Core 2.x,
                // but it is correct since .NET Core 3.0.0
                productVersion = Environment.Version.ToString();
            }

            if (productVersion == null)
            {
                try
                {
                    // try to get product version from assembly path
                    Match match = Regex.Match(
                        RootAssembly.CodeBase,
                        @"/[^/]*microsoft\.netcore\.app/(\d+\.\d+\.\d+[^/]*)/",
                        RegexOptions.IgnoreCase);

                    if (match.Success && match.Groups.Count > 0 && match.Groups[1].Success)
                    {
                        productVersion = match.Groups[1].Value;
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error getting .NET Core version from assembly path");
                }
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
