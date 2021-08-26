#if NETCOREAPP || NETSTANDARD

#define RUNTIMEINFORMATION_TYPE_AVAILABLE

#endif

using System;
using System.Reflection;

#if RUNTIMEINFORMATION_TYPE_AVAILABLE

using System.Runtime.InteropServices;

#endif

namespace Datadog.Util
{
    internal class RuntimeEnvironmentInfo
    {
        private const string CoreLibName_Mscorlib = "mscorlib";
        private const string CoreLibName_CoreLib = "System.Private.CoreLib";
        private const string UnknownMoniker = "Unknown";

        #region Nested Types

        public class CoreAssembyInformation
        {
            internal CoreAssembyInformation(bool isMscorlib, bool isSysPrivCoreLib, string name)
            {
                Validate.NotNull(name, nameof(name));
                this.IsMscorlib = isMscorlib;
                this.IsSysPrivCoreLib = isSysPrivCoreLib;
                this.Name = name;
            }

            public bool IsMscorlib { get; }
            public bool IsSysPrivCoreLib { get; }
            public string Name { get; }
        }

        #endregion Nested Types

        #region Static APIs

        private static RuntimeEnvironmentInfo s_singeltonInstance = null;

        public static RuntimeEnvironmentInfo SingeltonInstance
        {
            get
            {
                RuntimeEnvironmentInfo singeltonInstance = s_singeltonInstance;
                if (singeltonInstance == null)
                {
                    singeltonInstance = CreateNew();
                    s_singeltonInstance = singeltonInstance;    // benign race
                }

                return singeltonInstance;
            }
        }

        private static RuntimeEnvironmentInfo CreateNew()
        {
            try
            {
                Assembly objectTypeAssembly = (new object()).GetType().Assembly;
                string objectTypeAssemblyName = objectTypeAssembly.GetName()?.Name ?? UnknownMoniker;
                var coreAssembyInfo = new CoreAssembyInformation(isMscorlib: CoreLibName_Mscorlib.Equals(objectTypeAssemblyName, StringComparison.OrdinalIgnoreCase),
                                                                 isSysPrivCoreLib: CoreLibName_CoreLib.Equals(objectTypeAssemblyName, StringComparison.OrdinalIgnoreCase),
                                                                 name: objectTypeAssemblyName);

                string runtimeName = GetRuntimeName(coreAssembyInfo);
                string runtimeVersion = GetRuntimeVersion(coreAssembyInfo, objectTypeAssembly);
                string processArchitecture = GetProcessArchitecture();
                string osPlatform = GetOsPlatform();
                string osArchitecture = GetOsArchitecture();
                string osDescription = GetOsDescriptio();

                return new RuntimeEnvironmentInfo(runtimeName,
                                                  runtimeVersion,
                                                  processArchitecture,
                                                  osPlatform,
                                                  osArchitecture,
                                                  osDescription,
                                                  coreAssembyInfo);
            }
            catch
            {
                return new RuntimeEnvironmentInfo(runtimeName: UnknownMoniker,
                                                  runtimeVersion: UnknownMoniker,
                                                  processArchitecture: UnknownMoniker,
                                                  osPlatform: UnknownMoniker,
                                                  osArchitecture: UnknownMoniker,
                                                  osDescription: UnknownMoniker,
                                                  coreAssembyInfo: new CoreAssembyInformation(isMscorlib: false,
                                                                                              isSysPrivCoreLib: false,
                                                                                              name: UnknownMoniker));
            }
        }

        private static string GetRuntimeName(CoreAssembyInformation coreAssembyInfo)
        {
            string runtimeName = null;

#if RUNTIMEINFORMATION_TYPE_AVAILABLE
            // See https://docs.microsoft.com/en-us/dotnet/api/system.runtime.interopservices.runtimeinformation.frameworkdescription?view=net-5.0#remarks

            string frameworkDescription = RuntimeInformation.FrameworkDescription;
            if (frameworkDescription.StartsWith(".NET Native", StringComparison.OrdinalIgnoreCase))
            {
                runtimeName = ".NET Native";
            }
            else if (frameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                runtimeName = ".NET Framework";
            }
            else if (frameworkDescription.StartsWith(".NET Core", StringComparison.OrdinalIgnoreCase))
            {
                runtimeName = ".NET Core";
            }
            else if (frameworkDescription.StartsWith(".NET 5", StringComparison.OrdinalIgnoreCase))
            {
                runtimeName = ".NET 5";
            }
#endif
            if (runtimeName == null)
            {
                if (coreAssembyInfo.IsMscorlib)
                {
                    runtimeName = ".NET Framework";
                }
                else if (coreAssembyInfo.IsSysPrivCoreLib)
                {
                    runtimeName = $"Unknown {coreAssembyInfo.Name}-based .NET-compatible runtime";
                }
            }

            if (runtimeName == null)
            {
                runtimeName = $"Unknown .NET-compatible runtime (BCL: {coreAssembyInfo.Name})";
            }

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

        private static string GetOsDescriptio()
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
        #endregion Static APIs

        #region Non-Static APIs

        private string _stringView = null;
        private string _osPlatformMoniker = null;

        private RuntimeEnvironmentInfo(string runtimeName,
                                       string runtimeVersion,
                                       string processArchitecture,
                                       string osPlatform,
                                       string osArchitecture,
                                       string osDescription,
                                       CoreAssembyInformation coreAssembyInfo)
        {
            this.RuntimeName = runtimeName;
            this.RuntimeVersion = runtimeVersion;
            this.ProcessArchitecture = processArchitecture;
            this.OsPlatform = osPlatform;
            this.OsArchitecture = osArchitecture;
            this.OsDescription = osDescription;
            this.CoreAssembyInfo = coreAssembyInfo;
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
            string stringView = _stringView;
            if (stringView == null)
            {
                stringView = $"{RuntimeName} {RuntimeVersion} ({ProcessArchitecture}) running on {OsPlatform} {OsArchitecture}";

                if (!String.IsNullOrWhiteSpace(OsDescription))
                {
                    stringView = $"{stringView} ({OsDescription})";
                }

                _stringView = stringView;
            }

            return stringView;
        }

        public string GetOsPlatformMoniker()

        {
            string osPlatformMoniker = _osPlatformMoniker;
            if (osPlatformMoniker == null)
            {
                if (OsPlatform.StartsWith("win", StringComparison.OrdinalIgnoreCase))
                {
                    osPlatformMoniker = "win";
                }
                else if (OsPlatform.StartsWith("linux", StringComparison.OrdinalIgnoreCase))
                {
                    osPlatformMoniker = "linux";
                }
                else if (OsPlatform.StartsWith("macos", StringComparison.OrdinalIgnoreCase))
                {
                    osPlatformMoniker = "osx";
                }
                else
                {
                    osPlatformMoniker = OsPlatform.ToLower();
                }

                _osPlatformMoniker = osPlatformMoniker;
            }

            return osPlatformMoniker;
        }

        #endregion Non-Static APIs
    }
}
