// <copyright file="FrameworkDescription.NetFramework.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

#if NETFRAMEWORK
using System;
using System.Linq;
using Microsoft.Win32;

namespace Datadog.Trace
{
    internal partial class FrameworkDescription
    {
        private const string Unknown = "unknown";
        private static readonly Lazy<FrameworkDescription> _instance = new(Create);

        public static FrameworkDescription Instance => _instance.Value;

        public static FrameworkDescription Create()
        {
            var osArchitecture = Unknown;
            var processArchitecture = Unknown;
            var frameworkVersion = Unknown;
            var osDescription = Unknown;
            Version? runtimeVersion = null;

            try
            {
                osDescription = Environment.OSVersion.VersionString;
                osArchitecture = Environment.Is64BitOperatingSystem ? "x64" : "x86";
                processArchitecture = Environment.Is64BitProcess ? "x64" : "x86";
                GetNetFrameworkVersion(out frameworkVersion, out runtimeVersion);
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
                osDescription: osDescription,
                runtimeVersion ?? Environment.Version);
        }

        public bool IsCoreClr()
        {
            return false;
        }

        private static void GetNetFrameworkVersion(out string frameworkVersion, out Version version)
        {
            try
            {
                object? registryValue;

                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Default))
                using (var subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\"))
                {
                    registryValue = subKey?.GetValue("Release");
                }

                if (registryValue is int release)
                {
                    // find the known version on the list with the largest release number
                    // that is lower than or equal to the release number in the Windows Registry
                    if (GetDotNetFrameworkProductMapping(release, out var productVersion, out var runtime))
                    {
                        frameworkVersion = productVersion;
                        version = runtime;
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Error getting .NET Framework version from Windows Registry");
            }

            // if we fail to extract version from assembly path,
            // fall back to the [AssemblyInformationalVersion] or [AssemblyFileVersion]
            if (GetVersionFromAssemblyAttributes() is { } foundVersion)
            {
                frameworkVersion = foundVersion;
                if (Version.TryParse(foundVersion, out var parsedVersion))
                {
                    version = parsedVersion;
                }
                else
                {
                    version = Environment.Version;
                }

                return;
            }

            // at this point, everything else has failed (this is probably the same as [AssemblyFileVersion] above)
            version = Environment.Version;
            frameworkVersion = version.ToString();
        }
    }
}
#endif
