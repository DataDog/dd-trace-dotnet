using System;
using System.IO;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public static class TestHelper
    {
        public static string GetPlatform()
        {
            return Environment.Is64BitProcess ? "x64" : "x86";
        }

        public static string GetOS()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "win" :
                   Environment.OSVersion.Platform == PlatformID.Unix ? "linux" :
                   Environment.OSVersion.Platform == PlatformID.MacOSX ? "osx" :
                   string.Empty;
        }

        public static string GetRuntimeIdentifier()
        {
            return BuildParameters.CoreClr ? string.Empty : $"{GetOS()}-{GetPlatform()}";
        }

        public static string GetSolutionDirectory()
        {
            string currentDirectory = Environment.CurrentDirectory;

            int index = currentDirectory.Replace('\\', '/')
                                        .LastIndexOf("/test/", StringComparison.InvariantCultureIgnoreCase);

            return currentDirectory.Substring(0, index);
        }

        public static string GetProfilerDllPath()
        {
            return Path.Combine(
                GetSolutionDirectory(),
                "src",
                "Datadog.Trace.ClrProfiler.Native",
                "bin",
                BuildParameters.Configuration,
                GetPlatform(),
                "Datadog.Trace.ClrProfiler.Native." + (GetOS() == "win" ? "dll" : "so"));
        }

        public static string GetSampleApplicationPath(string sampleAppName)
        {
            string appFileName = BuildParameters.CoreClr ? $"Samples.{sampleAppName}.dll" : $"Samples.{sampleAppName}.exe";
            string binDir = Path.Combine(
                GetSolutionDirectory(),
                "samples",
                $"Samples.{sampleAppName}",
                "bin");

            if (GetOS() == "win")
            {
                return Path.Combine(
                    binDir,
                    GetPlatform(),
                    BuildParameters.Configuration,
                    BuildParameters.TargetFramework,
                    GetRuntimeIdentifier(),
                    appFileName);
            }
            else
            {
                return Path.Combine(
                    binDir,
                    BuildParameters.Configuration,
                    BuildParameters.TargetFramework,
                    GetRuntimeIdentifier(),
                    "publish",
                    appFileName);
            }
        }
    }
}
