using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public abstract class TestHelper
    {
        protected TestHelper(string sampleAppName, ITestOutputHelper output)
        {
            SampleAppName = sampleAppName;
            Output = output;

            Output.WriteLine($"Platform: {GetPlatform()}");
            Output.WriteLine($"Configuration: {BuildParameters.Configuration}");
            Output.WriteLine($"TargetFramework: {BuildParameters.TargetFramework}");
            Output.WriteLine($".NET Core: {BuildParameters.CoreClr}");
            Output.WriteLine($"Application: {GetSampleApplicationPath()}");
            Output.WriteLine($"Profiler DLL: {GetProfilerDllPath()}");
        }

        protected string SampleAppName { get; }

        protected ITestOutputHelper Output { get; }

        public string GetPlatform()
        {
            return Environment.Is64BitProcess ? "x64" : "x86";
        }

        public string GetOS()
        {
            return Environment.OSVersion.Platform == PlatformID.Win32NT ? "win" :
                   Environment.OSVersion.Platform == PlatformID.Unix    ? "linux" :
                   Environment.OSVersion.Platform == PlatformID.MacOSX  ? "osx" :
                                                                          string.Empty;
        }

        public string GetRuntimeIdentifier()
        {
            return BuildParameters.CoreClr ? string.Empty : $"{GetOS()}-{GetPlatform()}";
        }

        public string GetSolutionDirectory()
        {
            string[] pathParts = Environment.CurrentDirectory.ToLowerInvariant().Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int directoryDepth = pathParts.Length - pathParts.ToList().IndexOf("test");
            string relativeBasePath = string.Join("\\", Enumerable.Repeat("..", directoryDepth));
            return Path.GetFullPath(relativeBasePath);
        }

        public string GetProfilerDllPath()
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

        public string GetSampleApplicationPath()
        {
            string appFileName = BuildParameters.CoreClr ? $"Samples.{SampleAppName}.dll" : $"Samples.{SampleAppName}.exe";
            return Path.Combine(
                GetSolutionDirectory(),
                "samples",
                $"Samples.{SampleAppName}",
                "bin",
                GetPlatform(),
                BuildParameters.Configuration,
                BuildParameters.TargetFramework,
                GetRuntimeIdentifier(),
                appFileName);
        }

        public Process StartSample()
        {
            // get path to native profiler dll
            string profilerDllPath = GetProfilerDllPath();
            if (!File.Exists(profilerDllPath))
            {
                throw new Exception($"profiler not found: {profilerDllPath}");
            }

            // get path to sample app that the profiler will attach to
            string sampleAppPath = GetSampleApplicationPath();
            if (!File.Exists(sampleAppPath))
            {
                throw new Exception($"application not found: {sampleAppPath}");
            }

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*integrations.json").Select(Path.GetFullPath);

            return ProfilerHelper.StartProcessWithProfiler(
                sampleAppPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath);
        }

        public ProcessResult RunSampleAndWaitForExit()
        {
            Process process = StartSample();

            string standardOutput = process.StandardOutput.ReadToEnd();
            string standardError = process.StandardError.ReadToEnd();
            process.WaitForExit();
            int exitCode = process.ExitCode;

            if (!string.IsNullOrWhiteSpace(standardOutput))
            {
                Output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            }

            if (!string.IsNullOrWhiteSpace(standardError))
            {
                Output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");
            }

            return new ProcessResult(process, standardOutput, standardError, exitCode);
        }
    }
}
