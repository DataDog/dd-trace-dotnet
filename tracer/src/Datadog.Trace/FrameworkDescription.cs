// <copyright file="FrameworkDescription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal sealed partial class FrameworkDescription
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FrameworkDescription));

        private static readonly Assembly RootAssembly = typeof(object).Assembly;

        private FrameworkDescription(
            string name,
            string productVersion,
            string osPlatform,
            string osArchitecture,
            string processArchitecture,
            string osDescription,
            Version runtimeVersion)
        {
            Name = name;
            ProductVersion = productVersion;
            OSPlatform = osPlatform;
            OSArchitecture = osArchitecture;
            OSDescription = osDescription;
            ProcessArchitecture = processArchitecture;
            RuntimeVersion = runtimeVersion;
        }

        public string Name { get; }

        public string ProductVersion { get; }

        public string OSPlatform { get; }

        public string OSArchitecture { get; }

        public string ProcessArchitecture { get; }

        public string OSDescription { get; }

        public Version RuntimeVersion { get; }

        public static bool IsNet5()
        {
            return Environment.Version.Major >= 5;
        }

        public bool IsWindows()
        {
            return string.Equals(OSPlatform, OSPlatformName.Windows, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            // examples:
            // .NET Framework 4.8 x86 on Windows x64
            // .NET Core 3.0.0 x64 on Linux x64
            return $"{Name} {ProductVersion} {ProcessArchitecture} on {OSPlatform} {OSArchitecture}";
        }

        private static string? GetVersionFromAssemblyAttributes()
        {
            string? productVersion = null;

            try
            {
                // if we fail to extract version from assembly path, fall back to the [AssemblyInformationalVersion],
                var informationalVersionAttribute = (AssemblyInformationalVersionAttribute?)RootAssembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));

                // split remove the commit hash from pre-release versions
                productVersion = informationalVersionAttribute?.InformationalVersion?.Split('+')[0];
            }
            catch (Exception e)
            {
                Log.Error(e, "Error getting framework version from [AssemblyInformationalVersion]");
            }

            if (productVersion == null)
            {
                try
                {
                    // and if that fails, try [AssemblyFileVersion]
                    var fileVersionAttribute = (AssemblyFileVersionAttribute?)RootAssembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute));
                    productVersion = fileVersionAttribute?.Version;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error getting framework version from [AssemblyFileVersion]");
                }
            }

            return productVersion;
        }

        private static bool GetDotNetFrameworkProductMapping(
            int release,
            [NotNullWhen(true)] out string? productVersion,
            [NotNullWhen(true)] out Version? runtimeVersion)
        {
            switch (release)
            {
                // known min value for each framework version
                case >= 533325:
                    productVersion = "4.8.1";
                    runtimeVersion = new Version(4, 8, 1);
                    return true;
                case >= 528040:
                    productVersion = "4.8";
                    runtimeVersion = new Version(4, 8);
                    return true;
                case >= 461808:
                    productVersion = "4.7.2";
                    runtimeVersion = new Version(4, 7, 2);
                    return true;
                case >= 461308:
                    productVersion = "4.7.1";
                    runtimeVersion = new Version(4, 7, 1);
                    return true;
                case >= 460798:
                    productVersion = "4.7";
                    runtimeVersion = new Version(4, 7);
                    return true;
                case >= 394802:
                    productVersion = "4.6.2";
                    runtimeVersion = new Version(4, 6, 2);
                    return true;
                case >= 394254:
                    productVersion = "4.6.1";
                    runtimeVersion = new Version(4, 6, 1);
                    return true;
                case >= 393295:
                    productVersion = "4.6";
                    runtimeVersion = new Version(4, 6);
                    return true;
                case >= 379893:
                    productVersion = "4.5.2";
                    runtimeVersion = new Version(4, 5, 2);
                    return true;
                case >= 378675:
                    productVersion = "4.5.1";
                    runtimeVersion = new Version(4, 5, 1);
                    return true;
                case >= 378389:
                    productVersion = "4.5";
                    runtimeVersion = new Version(4, 5);
                    return true;
            }

            productVersion = null;
            runtimeVersion = null;
            return false;
        }
    }
}
