using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Datadog.Core.Tools
{
    /// <summary>
    /// General use utility methods for all tests and tools.
    /// </summary>
    public class EnvironmentTools
    {
        public const string ProfilerClsId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        public const string DotNetFramework = ".NETFramework";
        public const string CoreFramework = ".NETCoreApp";

        private static string _solutionDirectory = null;

        /// <summary>
        /// Find the solution directory from anywhere in the hierarchy.
        /// </summary>
        /// <returns>The solution directory.</returns>
        public static string GetSolutionDirectory()
        {
            if (_solutionDirectory == null)
            {
                var startDirectory = Environment.CurrentDirectory;
                var currentDirectory = Directory.GetParent(startDirectory);
                const string searchItem = @"Datadog.Trace.sln";

                while (true)
                {
                    var slnFile = currentDirectory.GetFiles(searchItem).SingleOrDefault();

                    if (slnFile != null)
                    {
                        break;
                    }

                    currentDirectory = currentDirectory.Parent;

                    if (currentDirectory == null || !currentDirectory.Exists)
                    {
                        throw new Exception($"Unable to find solution directory from: {startDirectory}");
                    }
                }

                _solutionDirectory = currentDirectory.FullName;
            }

            return _solutionDirectory;
        }

        public static string GetTracerVersion()
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return $"{TracerVersion.Major}.{TracerVersion.Minor}.{TracerVersion.Patch}{(TracerVersion.IsPreRelease ? "-prerelease" : string.Empty)}";
        }

        public static string GetOS()
        {
            return IsWindows() ? "win" :
                   RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                   RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                                                                       string.Empty;
        }

        public static bool IsWindows()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }

        public static string GetPlatform()
        {
            return RuntimeInformation.ProcessArchitecture.ToString();
        }

        public static string GetBuildConfiguration()
        {
#if DEBUG
            return "Debug";
#else
        return "Release";
#endif
        }

        public static bool IsConfiguredToProfile(Type anchorType)
        {
            var anchorAssembly = Assembly.GetAssembly(anchorType);
            var targetFramework = anchorAssembly.GetCustomAttribute<TargetFrameworkAttribute>();

            var parts = targetFramework.FrameworkName.Split(',');
            var runtime = parts[0];
            var isCoreClr = runtime.Equals(CoreFramework);

            var environmentVariables = Environment.GetEnvironmentVariables();

            var prefix = "COR";

            if (isCoreClr)
            {
                prefix = "CORECLR";
            }

            if ((string)environmentVariables[$"{prefix}_ENABLE_PROFILING"] != "1")
            {
                return false;
            }

            if ((string)environmentVariables[$"{prefix}_PROFILER"] != ProfilerClsId)
            {
                return false;
            }

            var profilerPath = (string)environmentVariables[$"{prefix}_PROFILER_PATH"];

            if (!File.Exists(profilerPath))
            {
                return false;
            }

            return true;
        }
    }
}
