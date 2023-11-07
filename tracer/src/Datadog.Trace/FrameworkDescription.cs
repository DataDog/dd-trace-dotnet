// <copyright file="FrameworkDescription.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Microsoft.Win32;

namespace Datadog.Trace
{
    internal partial class FrameworkDescription
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(FrameworkDescription));

        private static readonly Assembly RootAssembly = typeof(object).Assembly;

        private static readonly Tuple<int, string>[] DotNetFrameworkVersionMapping =
        {
            // known min value for each framework version
            Tuple.Create(533325, "4.8.1"),
            Tuple.Create(528040, "4.8"),
            Tuple.Create(461808, "4.7.2"),
            Tuple.Create(461308, "4.7.1"),
            Tuple.Create(460798, "4.7"),
            Tuple.Create(394802, "4.6.2"),
            Tuple.Create(394254, "4.6.1"),
            Tuple.Create(393295, "4.6"),
            Tuple.Create(379893, "4.5.2"),
            Tuple.Create(378675, "4.5.1"),
            Tuple.Create(378389, "4.5"),
        };

        private FrameworkDescription(
            string name,
            string productVersion,
            string osPlatform,
            string osArchitecture,
            string processArchitecture,
            string osDescription)
        {
            Name = name;
            ProductVersion = productVersion;
            OSPlatform = osPlatform;
            OSArchitecture = osArchitecture;
            OSDescription = osDescription;
            ProcessArchitecture = processArchitecture;
        }

        public string Name { get; }

        public string ProductVersion { get; }

        public string OSPlatform { get; }

        public string OSArchitecture { get; }

        public string ProcessArchitecture { get; }

        public string OSDescription { get; }

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

        private static string GetVersionFromAssemblyAttributes()
        {
            string productVersion = null;

            try
            {
                // if we fail to extract version from assembly path, fall back to the [AssemblyInformationalVersion],
                var informationalVersionAttribute = (AssemblyInformationalVersionAttribute)RootAssembly.GetCustomAttribute(typeof(AssemblyInformationalVersionAttribute));

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
                    var fileVersionAttribute = (AssemblyFileVersionAttribute)RootAssembly.GetCustomAttribute(typeof(AssemblyFileVersionAttribute));
                    productVersion = fileVersionAttribute?.Version;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error getting framework version from [AssemblyFileVersion]");
                }
            }

            return productVersion;
        }
    }
}
