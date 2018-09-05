using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Datadog.Trace.TestHelpers;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class ConsoleCoreTests
    {
        private readonly ITestOutputHelper _output;

        public ConsoleCoreTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1025:CodeMustNotContainMultipleWhitespaceInARow", Justification = "Reviewed.")]
        public void ProfilerAttached()
        {
            var platform = Environment.Is64BitProcess ? "x64" : "x86";

            var os = Environment.OSVersion.Platform == PlatformID.Win32NT ? "win" :
                     Environment.OSVersion.Platform == PlatformID.Unix    ? "linux" :
                     Environment.OSVersion.Platform == PlatformID.MacOSX  ? "osx" :
                                                                            string.Empty;

            var runtimeIdentifier = BuildParameters.CoreClr ? string.Empty : $"{os}-{platform}";

            string[] pathParts = Environment.CurrentDirectory.ToLowerInvariant().Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int directoryDepth = pathParts.Length - pathParts.ToList().IndexOf("test");
            string relativeBasePath = string.Join("\\", Enumerable.Repeat("..", directoryDepth));
            string absoluteBasePath = Path.GetFullPath(relativeBasePath);

            // get path to native profiler dll
            string profilerDllPath = $@"{absoluteBasePath}\src\Datadog.Trace.ClrProfiler.Native\bin\{BuildParameters.Configuration}\{platform}\Datadog.Trace.ClrProfiler.Native.dll";
            Assert.True(File.Exists(profilerDllPath), $"Profiler DLL not found at {profilerDllPath}");

            // get path to sample app that the profiler will attach to
            string appBasePath = $@"{absoluteBasePath}\samples\Samples.ConsoleCore\bin\{platform}\{BuildParameters.Configuration}\{BuildParameters.TargetFramework}\{runtimeIdentifier}";
            string appFileName = BuildParameters.CoreClr ? $@"{appBasePath}\Samples.ConsoleCore.dll" : $@"{appBasePath}\Samples.ConsoleCore.exe";
            string appPath = Path.Combine(appBasePath, appFileName);
            Assert.True(File.Exists(appPath), $"Application not found at {appPath}");

            // get full paths to integration definitions
            IEnumerable<string> integrationPaths = Directory.EnumerateFiles(".", "*.json").Select(Path.GetFullPath);

            string standardOutput;
            string standardError;
            int exitCode;

            _output.WriteLine($"Platform: {platform}");
            _output.WriteLine($"Configuration: {BuildParameters.Configuration}");
            _output.WriteLine($"TargetFramework: {BuildParameters.TargetFramework}");
            _output.WriteLine($".NET Core: {BuildParameters.CoreClr}");
            _output.WriteLine($"Application: {appPath}");
            _output.WriteLine($"Profiler DLL: {profilerDllPath}");

            using (Process process = ProfilerHelper.StartProcessWithProfiler(
                appPath,
                BuildParameters.CoreClr,
                integrationPaths,
                Instrumentation.ProfilerClsid,
                profilerDllPath))
            {
                standardOutput = process.StandardOutput.ReadToEnd();
                standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();
                exitCode = process.ExitCode;
            }

            _output.WriteLine($"StandardOutput:{Environment.NewLine}{standardOutput}");
            _output.WriteLine($"StandardError:{Environment.NewLine}{standardError}");

            Assert.True(exitCode >= 0, $"Process exited with code {exitCode}");

            dynamic output = JsonConvert.DeserializeObject(standardOutput);
            Assert.True((bool)output.ProfilerAttached);
            Assert.Equal(6, (int)output.AddResult);
        }
    }
}
