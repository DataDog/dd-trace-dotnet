using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TestHelper
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
            string[] pathParts = Environment.CurrentDirectory.ToLowerInvariant().Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int directoryDepth = pathParts.Length - pathParts.ToList().IndexOf("test");
            string relativeBasePath = string.Join("\\", Enumerable.Repeat("..", directoryDepth));
            return Path.GetFullPath(relativeBasePath);
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
                "Datadog.Trace.ClrProfiler.Native.dll");
        }

        public static string GetSampleDllPath(string name)
        {
            string appFileName = BuildParameters.CoreClr ? $"Samples.{name}.dll" : $"Samples.{name}.exe";
            return Path.Combine(
                GetSolutionDirectory(),
                "samples",
                $"Samples.{name}",
                "bin",
                GetPlatform(),
                BuildParameters.Configuration,
                BuildParameters.TargetFramework,
                GetRuntimeIdentifier(),
                appFileName);
        }

        public static Process StartSample(string name)
        {
            // get path to native profiler dll
            if (!File.Exists(GetProfilerDllPath()))
            {
                throw new Exception($"profiler not found: {GetProfilerDllPath()}");
            }

            // get path to sample app that the profiler will attach to
            var sampleDllPath = GetSampleDllPath(name);
            if (!File.Exists(sampleDllPath))
            {
                throw new Exception($"application not found: {sampleDllPath}");
            }

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*.json").Select(Path.GetFullPath);

            return ProfilerHelper.StartProcessWithProfiler(
                sampleDllPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                GetProfilerDllPath());
        }
    }
}
