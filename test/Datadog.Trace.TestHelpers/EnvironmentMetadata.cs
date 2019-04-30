using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Datadog.Trace.ClrProfiler;

namespace Datadog.Trace.TestHelpers
{
    public class EnvironmentMetadata
    {
        public const string DotNetFramework = ".NETFramework";
        public const string CoreFramework = ".NETCoreApp";

        private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly();
        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
        private static readonly string RuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();

        private readonly int _major;
        private readonly int _minor;
        private readonly string _patch = null;

        private readonly string _runtime;
        private readonly bool _isCoreClr;
        private readonly object _anchor;
        private readonly Type _anchorType;
        private readonly Assembly _anchorAssembly;
        private readonly TargetFrameworkAttribute _targetFramework;

        private string _integrationsFileLocation;
        private string _profilerFileLocation;

        public EnvironmentMetadata(object anchor)
        {
            _anchor = anchor;
            _anchorType = _anchor.GetType();
            _anchorAssembly = Assembly.GetAssembly(_anchorType);
            _targetFramework = _anchorAssembly.GetCustomAttribute<TargetFrameworkAttribute>();

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
            return IsCoreClr() ? string.Empty : $"{EnvironmentMetadata.GetOS()}-{GetPlatform()}";
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

                var integrationsDirectory = GetOutputDirectory();

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
                string fileName = "Datadog.Trace.ClrProfiler.Native." + (IsWindows() ? "dll" : "so");

                var directory = GetOutputDirectory();

                _profilerFileLocation = Path.Combine(directory, fileName);

                if (!File.Exists(_profilerFileLocation))
                {
                    throw new Exception($"Missing {fileName} in output directory {directory}");
                }
            }

            return _profilerFileLocation;
        }

        public string GetOutputDirectory()
        {
            var anchorAssemblyLocation = _anchorAssembly.Location;

            int index = anchorAssemblyLocation.Replace('\\', '/')
                                              .LastIndexOf("/", StringComparison.OrdinalIgnoreCase);

            var outputDirectory = anchorAssemblyLocation.Substring(0, index);

            return outputDirectory;
        }

        public void SetEnvironmentVariableDefaults(int agentPort)
        {
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("CORECLR_PROFILER", Instrumentation.ProfilerClsid);
            Environment.SetEnvironmentVariable("CORECLR_PROFILER_PATH", GetProfilerPath());

            Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", "1");
            Environment.SetEnvironmentVariable("COR_PROFILER", Instrumentation.ProfilerClsid);
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
