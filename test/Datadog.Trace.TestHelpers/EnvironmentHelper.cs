using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class EnvironmentHelper
    {
        public const string ProfilerClsId = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";
        public const string DotNetFramework = ".NETFramework";
        public const string CoreFramework = ".NETCoreApp";

        private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly();
        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
        private static readonly string RuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();

        private readonly ITestOutputHelper _output;
        private readonly int _major;
        private readonly int _minor;
        private readonly string _patch = null;

        private readonly string _runtime;
        private readonly bool _isCoreClr;
        private readonly string _samplesDirectory;
        private readonly Type _anchorType;
        private readonly Assembly _anchorAssembly;
        private readonly TargetFrameworkAttribute _targetFramework;

        private string _integrationsFileLocation;
        private string _profilerFileLocation;

        public EnvironmentHelper(
            string sampleName,
            Type anchorType,
            ITestOutputHelper output,
            string samplesDirectory = "samples")
        {
            SampleName = sampleName;
            _samplesDirectory = samplesDirectory;
            _anchorType = anchorType;
            _anchorAssembly = Assembly.GetAssembly(_anchorType);
            _targetFramework = _anchorAssembly.GetCustomAttribute<TargetFrameworkAttribute>();
            _output = output;

            var parts = _targetFramework.FrameworkName.Split(',');
            _runtime = parts[0];
            _isCoreClr = _runtime.Equals(CoreFramework);

            var versionParts = parts[1].Replace("Version=v", string.Empty).Split('.');
            _major = int.Parse(versionParts[0]);
            _minor = int.Parse(versionParts[1]);

            if (versionParts.Length == 3)
            {
                _patch = versionParts[2];
            }
        }

        public string SampleName { get; }

        public static string GetExecutingAssembly()
        {
            return ExecutingAssembly.Location;
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

        public static bool IsCoreClr()
        {
            return RuntimeFrameworkDescription.Contains("core");
        }

        public static string GetRuntimeIdentifier()
        {
            return IsCoreClr() ? string.Empty : $"{EnvironmentHelper.GetOS()}-{GetPlatform()}";
        }

        public static string GetSolutionDirectory()
        {
            string currentDirectory = Environment.CurrentDirectory;

            int index = currentDirectory.Replace('\\', '/')
                                        .LastIndexOf("/test/", StringComparison.OrdinalIgnoreCase);

            return currentDirectory.Substring(0, index);
        }

        public string[] GetIntegrationsFilePaths()
        {
            if (_integrationsFileLocation == null)
            {
                string fileName = "integrations.json";

                var integrationsDirectory = GetSampleApplicationOutputDirectory();

                _integrationsFileLocation = Path.Combine(integrationsDirectory, fileName);

                if (!File.Exists(_integrationsFileLocation))
                {
                    throw new Exception($"Missing {fileName} in output directory {integrationsDirectory}");
                }
            }

            return new[]
            {
                _integrationsFileLocation
            };
        }

        public string GetProfilerPath()
        {
            if (_profilerFileLocation == null)
            {
                string extension = IsWindows()
                                        ? "dll"
                                        : "so";

                string fileName = $"Datadog.Trace.ClrProfiler.Native.{extension}";

                var directory = GetSampleApplicationOutputDirectory();

                var profilerRelativePath = Path.Combine(
                    GetBuildConfiguration(),
                    GetPlatform(),
                    fileName);

                _profilerFileLocation = Path.Combine(
                    directory,
                    profilerRelativePath);

                if (!File.Exists(_profilerFileLocation))
                {
                    _output.WriteLine($"Unable to find profiler: {_profilerFileLocation}");

                    _profilerFileLocation = Path.Combine(
                        Path.GetDirectoryName(ExecutingAssembly.Location) ?? throw new InvalidOperationException($"Executing assembly must be set within {nameof(EnvironmentHelper)}"),
                        fileName);
                }

                if (!File.Exists(_profilerFileLocation))
                {
                    throw new Exception($"Unable to find profiler: {_profilerFileLocation}");
                }
            }

            return _profilerFileLocation;
        }

        public string GetSampleApplicationPath()
        {
            var appFileName = EnvironmentHelper.IsCoreClr() ? $"Samples.{SampleName}.dll" : $"Samples.{SampleName}.exe";
            var sampleAppPath = Path.Combine(GetSampleApplicationOutputDirectory(), appFileName);
            return sampleAppPath;
        }

        public string GetSampleApplicationOutputDirectory()
        {
            var solutionDirectory = GetSolutionDirectory();
            var sampleDirectory = $"Samples.{SampleName}";

            var binDir = Path.Combine(
                solutionDirectory,
                _samplesDirectory,
                sampleDirectory,
                "bin");

            string outputDir;

            if (EnvironmentHelper.GetOS() == "win")
            {
                outputDir = Path.Combine(
                    binDir,
                    GetPlatform(),
                    GetBuildConfiguration(),
                    GetTargetFramework(),
                    EnvironmentHelper.GetRuntimeIdentifier());
            }
            else
            {
                outputDir = Path.Combine(
                    binDir,
                    GetBuildConfiguration(),
                    GetTargetFramework(),
                    EnvironmentHelper.GetRuntimeIdentifier(),
                    "publish");
            }

            return outputDir;
        }

        public void SetEnvironmentVariableDefaults(int agentPort)
        {
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", ProfilerClsId);
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", GetProfilerPath());

            Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("COR_PROFILER", ProfilerClsId);
            Environment.SetEnvironmentVariable("COR_PROFILER_PATH", GetProfilerPath());

            Environment.SetEnvironmentVariable("DD_PROFILER_PROCESSES", GetExecutingAssembly());

            string integrations = string.Join(";", GetIntegrationsFilePaths());
            Environment.SetEnvironmentVariable("DD_INTEGRATIONS", integrations);
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_HOSTNAME", "localhost");
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_PORT", agentPort.ToString());

            // for ASP.NET Core sample apps, set the server's port
            Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://localhost:{agentPort}/");

            foreach (var name in new string[] { "REDIS_HOST" })
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(value))
                {
                    Environment.SetEnvironmentVariable(name, value);
                }
            }
        }

        public string GetTargetFramework()
        {
            if (_isCoreClr)
            {
                return $"netcoreapp{_major}.{_minor}";
            }

            return $"net{_major}{_minor}{_patch ?? string.Empty}";
        }
    }
}
