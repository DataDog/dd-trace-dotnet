// <copyright file="FrameworkDescription.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Linq;
using Datadog.Trace.Logging;
using Microsoft.Win32;

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
            var osArchitecture = "unknown";
            var processArchitecture = "unknown";
            var frameworkVersion = "unknown";
            var osDescription = "unknown";

            try
            {
                osDescription = Environment.OSVersion.VersionString;
                osArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                processArchitecture = Environment.Is64BitProcess ? "x64" : "x86";
                frameworkVersion = GetNetFrameworkVersion();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting framework description.");
            }

            return new FrameworkDescription(
                name: ".NET Framework",
                productVersion: frameworkVersion,
                osPlatform: "Windows",
                osArchitecture: osArchitecture,
                processArchitecture: processArchitecture,
                osDescription: osDescription);
        }

        public bool IsCoreClr()
        {
            return false;
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
                Log.Error(e, "Error getting .NET Framework version from Windows Registry");
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
