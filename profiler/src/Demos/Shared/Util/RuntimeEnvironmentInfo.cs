// <copyright file="RuntimeEnvironmentInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Reflection;

namespace Datadog.Demos.Util
{
    public class RuntimeEnvironmentInfo
    {
        public static readonly RuntimeEnvironmentInfo Instance = CreateNew();

        private const string Mscorlib = "mscorlib";
        private const string CoreLib = "System.Private.CoreLib";
        private const string UnknownMoniker = "Unknown";

        private string _stringView;

        private RuntimeEnvironmentInfo(
            string runtimeName,
            string runtimeVersion,
            string processArchitecture,
            string osPlatform,
            string osArchitecture,
            string osDescription,
            CoreAssembyInformation coreAssembyInfo)
        {
            RuntimeName = runtimeName;
            RuntimeVersion = runtimeVersion;
            ProcessArchitecture = processArchitecture;
            OsPlatform = osPlatform;
            OsArchitecture = osArchitecture;
            OsDescription = osDescription;
            CoreAssembyInfo = coreAssembyInfo;
        }

        public string RuntimeName { get; }
        public string RuntimeVersion { get; }
        public string ProcessArchitecture { get; }
        public string OsPlatform { get; }
        public string OsArchitecture { get; }
        public string OsDescription { get; }
        public CoreAssembyInformation CoreAssembyInfo { get; }

        public override string ToString()
        {
            var stringView = _stringView;

            if (stringView == null)
            {
                stringView = $"{RuntimeName} {RuntimeVersion} ({ProcessArchitecture}) running on {OsPlatform} {OsArchitecture}";

                if (!string.IsNullOrWhiteSpace(OsDescription))
                {
                    stringView = $"{stringView} ({OsDescription})";
                }

                _stringView = stringView;
            }

            return stringView;
        }

        private static RuntimeEnvironmentInfo CreateNew()
        {
            try
            {
                var objectTypeAssembly = typeof(object).Assembly;
                var objectTypeAssemblyName = objectTypeAssembly.GetName().Name ?? UnknownMoniker;
                var coreAssembyInfo = new CoreAssembyInformation(
                        isMscorlib: Mscorlib.Equals(objectTypeAssemblyName, StringComparison.OrdinalIgnoreCase),
                        isSysPrivCoreLib: CoreLib.Equals(objectTypeAssemblyName, StringComparison.OrdinalIgnoreCase),
                        name: objectTypeAssemblyName);

                var runtimeName = GetRuntimeName(coreAssembyInfo);
                var runtimeVersion = GetRuntimeVersion(coreAssembyInfo, objectTypeAssembly);
                var processArchitecture = GetProcessArchitecture();
                var osPlatform = GetOsPlatform();
                var osArchitecture = GetOsArchitecture();
                var osDescription = GetOsDescription();

                return new RuntimeEnvironmentInfo(
                            runtimeName,
                            runtimeVersion,
                            processArchitecture,
                            osPlatform,
                            osArchitecture,
                            osDescription,
                            coreAssembyInfo);
            }
            catch
            {
                return new RuntimeEnvironmentInfo(
                            runtimeName: UnknownMoniker,
                            runtimeVersion: UnknownMoniker,
                            processArchitecture: UnknownMoniker,
                            osPlatform: UnknownMoniker,
                            osArchitecture: UnknownMoniker,
                            osDescription: UnknownMoniker,
                            coreAssembyInfo: new CoreAssembyInformation(isMscorlib: false, isSysPrivCoreLib: false, name: UnknownMoniker));
            }
        }

        private static string GetRuntimeName(CoreAssembyInformation coreAssembyInfo)
        {
            string runtimeName;

#if NETSTANDARD || NETCOREAPP2_1_OR_GREATER
            // See https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription?view=net-5.0#remarks

            runtimeName = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
#else
            if (coreAssembyInfo.IsMscorlib)
            {
                runtimeName = ".NET Framework";
            }
            else if (coreAssembyInfo.IsSysPrivCoreLib)
            {
                runtimeName = $"Unknown {coreAssembyInfo.Name}-based .NET-compatible runtime";
            }
            else
            {
                runtimeName = $"Unknown .NET-compatible runtime (BCL: {coreAssembyInfo.Name})";
            }
#endif

            return runtimeName;
        }

        private static string GetRuntimeVersion(CoreAssembyInformation coreAssembyInfo, Assembly objectTypeAssembly)
        {
            Version environmentVersion = Environment.Version;

            if (coreAssembyInfo.IsSysPrivCoreLib && (environmentVersion.Major == 3 || environmentVersion.Major >= 5))
            {
                // On Net Core, Environment.Version returns "4.x" in .NET Core 2.x, but it is correct since .NET Core 3.0.0.
                string runtimeVersion = environmentVersion.ToString();
                return runtimeVersion;
            }

            string assemblyInfoVer = objectTypeAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (assemblyInfoVer != null)
            {
                // Strip the git hash if there is one:
                int plusIndex = assemblyInfoVer.IndexOf('+');
                if (plusIndex >= 0)
                {
                    assemblyInfoVer = assemblyInfoVer.Substring(0, plusIndex);
                }

                // Servicing version if there is one:
                int minusIndex = assemblyInfoVer.IndexOf('-');
                if (minusIndex >= 0)
                {
                    assemblyInfoVer = assemblyInfoVer.Substring(0, minusIndex);
                }

                string runtimeVersion = assemblyInfoVer.Trim();
                if (runtimeVersion.Length > 0)
                {
                    return runtimeVersion;
                }
            }

            return UnknownMoniker;
        }

        private static string GetProcessArchitecture()
        {
#if RUNTIMEINFORMATION_TYPE_AVAILABLE
            return ArchitectureToString(RuntimeInformation.ProcessArchitecture);
#else
            return Environment.Is64BitProcess ? "x64" : "x86";
#endif
        }

        private static string GetOsPlatform()
        {
#if RUNTIMEINFORMATION_TYPE_AVAILABLE
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "Windows";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "Linux";
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "MacOS";
            }
#endif
            return Environment.OSVersion.Platform.ToString();
        }

        private static string GetOsArchitecture()
        {
#if RUNTIMEINFORMATION_TYPE_AVAILABLE
            return ArchitectureToString(RuntimeInformation.OSArchitecture);
#else
            return Environment.Is64BitOperatingSystem ? "x64" : "x86";
#endif
        }

        private static string GetOsDescription()
        {
#if RUNTIMEINFORMATION_TYPE_AVAILABLE
            string osDescription = RuntimeInformation.OSDescription;
            if (!String.IsNullOrWhiteSpace(osDescription))
            {
                return osDescription;
            }
#endif
            return String.Empty;
        }

#if RUNTIMEINFORMATION_TYPE_AVAILABLE
        private static string ArchitectureToString(Architecture architecture)
        {
            switch (architecture)
            {
                case Architecture.X86:
                    return "x86";

                case Architecture.X64:
                    return "x64";

                case Architecture.Arm:
                    return "Arm";

                case Architecture.Arm64:
                    return "Arm64";

                default:
                    return architecture.ToString();
            }
        }
#endif

        public class CoreAssembyInformation
        {
            internal CoreAssembyInformation(bool isMscorlib, bool isSysPrivCoreLib, string name)
            {
                IsMscorlib = isMscorlib;
                IsSysPrivCoreLib = isSysPrivCoreLib;
                Name = name;
            }

            public bool IsMscorlib { get; }
            public bool IsSysPrivCoreLib { get; }
            public string Name { get; }
        }
    }
}
