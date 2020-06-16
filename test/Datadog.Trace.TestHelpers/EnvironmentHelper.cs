using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Datadog.Core.Tools;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class EnvironmentHelper
    {
        private static readonly Assembly EntryAssembly = Assembly.GetEntryAssembly();
        private static readonly Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
        private static readonly string RuntimeFrameworkDescription = RuntimeInformation.FrameworkDescription.ToLower();

        private readonly ITestOutputHelper _output;
        private readonly int _major;
        private readonly int _minor;
        private readonly string _patch = null;

        private readonly string _appNamePrepend;
        private readonly string _runtime;
        private readonly bool _isCoreClr;
        private readonly string _samplesDirectory;
        private readonly Type _anchorType;
        private readonly Assembly _anchorAssembly;
        private readonly TargetFrameworkAttribute _targetFramework;

        private bool _requiresProfiling;
        private string _integrationsFileLocation;
        private string _profilerFileLocation;

        public EnvironmentHelper(
            string sampleName,
            Type anchorType,
            ITestOutputHelper output,
            string samplesDirectory = "samples",
            bool prependSamplesToAppName = true,
            bool requiresProfiling = true)
        {
            SampleName = sampleName;
            _samplesDirectory = samplesDirectory ?? "samples";
            _anchorType = anchorType;
            _targetFramework = Assembly.GetAssembly(anchorType).GetCustomAttribute<TargetFrameworkAttribute>();
            _anchorAssembly = Assembly.GetAssembly(_anchorType);
            _targetFramework = Assembly.GetAssembly(anchorType).GetCustomAttribute<TargetFrameworkAttribute>();
            _output = output;
            _requiresProfiling = requiresProfiling;

            var parts = _targetFramework.FrameworkName.Split(',');
            _runtime = parts[0];
            _isCoreClr = _runtime.Equals(EnvironmentTools.CoreFramework);

            var versionParts = parts[1].Replace("Version=v", string.Empty).Split('.');
            _major = int.Parse(versionParts[0]);
            _minor = int.Parse(versionParts[1]);

            if (versionParts.Length == 3)
            {
                _patch = versionParts[2];
            }

            _appNamePrepend = prependSamplesToAppName
                          ? "Samples."
                          : string.Empty;
        }

        public bool DebugModeEnabled { get; set; }

        public Dictionary<string, string> CustomEnvironmentVariables { get; set; } = new Dictionary<string, string>();

        public string SampleName { get; }

        public string FullSampleName => $"{_appNamePrepend}{SampleName}";

        public static EnvironmentHelper NonProfiledHelper(Type anchor, string appName, string directory)
        {
            return new EnvironmentHelper(
                sampleName: appName,
                anchorType: anchor,
                output: null,
                samplesDirectory: directory,
                prependSamplesToAppName: false,
                requiresProfiling: false);
        }

        public static string GetExecutingAssembly()
        {
            return ExecutingAssembly.Location;
        }

        public static bool IsCoreClr()
        {
            return RuntimeFrameworkDescription.Contains("core");
        }

        public static string GetRuntimeIdentifier()
        {
            return IsCoreClr()
                       ? string.Empty
                       : $"{EnvironmentTools.GetOS()}-{EnvironmentTools.GetPlatform()}";
        }

        public static string GetSolutionDirectory()
        {
            return EnvironmentTools.GetSolutionDirectory();
        }

        public static void ClearProfilerEnvironmentVariables()
        {
            var environmentVariables = new[]
            {
                // .NET Core
                "CORECLR_ENABLE_PROFILING",
                "CORECLR_PROFILER",
                "CORECLR_PROFILER_PATH",
                "CORECLR_PROFILER_PATH_32",
                "CORECLR_PROFILER_PATH_64",

                // .NET Framework
                "COR_ENABLE_PROFILING",
                "COR_PROFILER",
                "COR_PROFILER_PATH",

                // Datadog
                "DD_PROFILER_PROCESSES",
                "DD_DOTNET_TRACER_HOME",
                "DD_INTEGRATIONS",
                "DD_DISABLED_INTEGRATIONS",
                "DD_SERVICE",
                "DD_VERSION",
                "DD_TAGS"
            };

            foreach (string variable in environmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        public void SetEnvironmentVariables(
            int agentPort,
            int aspNetCorePort,
            string processPath,
            StringDictionary environmentVariables)
        {
            var processName = processPath;
            string profilerEnabled = _requiresProfiling ? "1" : "0";
            string profilerPath;

            if (IsCoreClr())
            {
                environmentVariables["CORECLR_ENABLE_PROFILING"] = profilerEnabled;
                environmentVariables["CORECLR_PROFILER"] = EnvironmentTools.ProfilerClsId;

                profilerPath = GetProfilerPath();
                environmentVariables["CORECLR_PROFILER_PATH"] = profilerPath;
                environmentVariables["DD_DOTNET_TRACER_HOME"] = Path.GetDirectoryName(profilerPath);
            }
            else
            {
                environmentVariables["COR_ENABLE_PROFILING"] = profilerEnabled;
                environmentVariables["COR_PROFILER"] = EnvironmentTools.ProfilerClsId;

                profilerPath = GetProfilerPath();
                environmentVariables["COR_PROFILER_PATH"] = profilerPath;
                environmentVariables["DD_DOTNET_TRACER_HOME"] = Path.GetDirectoryName(profilerPath);

                processName = Path.GetFileName(processPath);
            }

            if (DebugModeEnabled)
            {
                environmentVariables["DD_TRACE_DEBUG"] = "1";
            }

            environmentVariables["DD_PROFILER_PROCESSES"] = processName;

            string integrations = string.Join(";", GetIntegrationsFilePaths());
            environmentVariables["DD_INTEGRATIONS"] = integrations;
            environmentVariables["DD_TRACE_AGENT_HOSTNAME"] = "localhost";
            environmentVariables["DD_TRACE_AGENT_PORT"] = agentPort.ToString();

            // for ASP.NET Core sample apps, set the server's port
            environmentVariables["ASPNETCORE_URLS"] = $"http://localhost:{aspNetCorePort}/";

            foreach (var name in new[] { "SERVICESTACK_REDIS_HOST", "STACKEXCHANGE_REDIS_HOST" })
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(value))
                {
                    environmentVariables[name] = value;
                }
            }

            foreach (var key in CustomEnvironmentVariables.Keys)
            {
                environmentVariables[key] = CustomEnvironmentVariables[key];
            }
        }

        public string[] GetIntegrationsFilePaths()
        {
            if (_integrationsFileLocation == null)
            {
                string fileName = "integrations.json";

                var directory = GetSampleApplicationOutputDirectory();

                var relativePath = Path.Combine(
                    "profiler-lib",
                    fileName);

                _integrationsFileLocation = Path.Combine(
                    directory,
                    relativePath);

                // TODO: get rid of the fallback options when we have a consistent convention

                if (!File.Exists(_integrationsFileLocation))
                {
                    _output?.WriteLine($"Attempt 1: Unable to find integrations at {_integrationsFileLocation}.");
                    // Let's try the executing directory, as dotnet publish ignores the Copy attributes we currently use
                    _integrationsFileLocation = Path.Combine(
                        GetExecutingProjectBin(),
                        relativePath);
                }

                if (!File.Exists(_integrationsFileLocation))
                {
                    _output?.WriteLine($"Attempt 2: Unable to find integrations at {_integrationsFileLocation}.");
                    // One last attempt at the solution root
                    _integrationsFileLocation = Path.Combine(
                        GetSolutionDirectory(),
                        fileName);
                }

                if (!File.Exists(_integrationsFileLocation))
                {
                    throw new Exception($"Attempt 3: Unable to find integrations at {_integrationsFileLocation}");
                }

                _output?.WriteLine($"Found integrations at {_integrationsFileLocation}.");
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
                string extension = EnvironmentTools.IsWindows()
                                       ? "dll"
                                       : "so";

                string fileName = $"Datadog.Trace.ClrProfiler.Native.{extension}";

                var directory = GetSampleApplicationOutputDirectory();

                var relativePath = Path.Combine(
                    "profiler-lib",
                    fileName);

                _profilerFileLocation = Path.Combine(
                    directory,
                    relativePath);

                // TODO: get rid of the fallback options when we have a consistent convention

                if (!File.Exists(_profilerFileLocation))
                {
                    _output?.WriteLine($"Attempt 1: Unable to find profiler at {_profilerFileLocation}.");
                    // Let's try the executing directory, as dotnet publish ignores the Copy attributes we currently use
                    _profilerFileLocation = Path.Combine(
                        GetExecutingProjectBin(),
                        relativePath);
                }

                if (!File.Exists(_profilerFileLocation))
                {
                    _output?.WriteLine($"Attempt 2: Unable to find profiler at {_profilerFileLocation}.");
                    // One last attempt at the actual native project directory
                    _profilerFileLocation = Path.Combine(
                        GetProfilerProjectBin(),
                        fileName);
                }

                if (!File.Exists(_profilerFileLocation))
                {
                    throw new Exception($"Attempt 3: Unable to find profiler at {_profilerFileLocation}");
                }

                _output?.WriteLine($"Found profiler at {_profilerFileLocation}.");
            }

            return _profilerFileLocation;
        }

        public string GetSampleApplicationPath(string packageVersion = "", string framework = "")
        {
            string extension = "exe";

            if (EnvironmentHelper.IsCoreClr() || _samplesDirectory.Contains("aspnet"))
            {
                extension = "dll";
            }

            var appFileName = $"{FullSampleName}.{extension}";
            var sampleAppPath = Path.Combine(GetSampleApplicationOutputDirectory(packageVersion: packageVersion, framework: framework), appFileName);
            return sampleAppPath;
        }

        public string GetSampleExecutionSource()
        {
            string executor;

            if (_samplesDirectory.Contains("aspnet"))
            {
                executor = $"C:\\Program Files{(Environment.Is64BitProcess ? string.Empty : " (x86)")}\\IIS Express\\iisexpress.exe";
            }
            else if (IsCoreClr())
            {
                executor = EnvironmentTools.IsWindows() ? "dotnet.exe" : "dotnet";
            }
            else
            {
                var appFileName = $"{FullSampleName}.exe";
                executor = Path.Combine(GetSampleApplicationOutputDirectory(), appFileName);

                if (!File.Exists(executor))
                {
                    throw new Exception($"Unable to find executing assembly at {executor}");
                }
            }

            return executor;
        }

        public string GetSampleProjectDirectory()
        {
            var solutionDirectory = GetSolutionDirectory();
            var projectDir = Path.Combine(
                solutionDirectory,
                _samplesDirectory,
                $"{FullSampleName}");
            return projectDir;
        }

        public string GetSampleApplicationOutputDirectory(string packageVersion = "", string framework = "")
        {
            var targetFramework = string.IsNullOrEmpty(framework) ? GetTargetFramework() : framework;
            var binDir = Path.Combine(
                GetSampleProjectDirectory(),
                "bin");

            string outputDir;

            if (_samplesDirectory.Contains("aspnet"))
            {
                outputDir = binDir;
            }
            else if (EnvironmentTools.GetOS() == "win")
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetPlatform(),
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework);
            }
            else
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework,
                    "publish");
            }

            return outputDir;
        }

        public string GetTargetFramework()
        {
            if (_isCoreClr)
            {
                return $"netcoreapp{_major}.{_minor}";
            }

            return $"net{_major}{_minor}{_patch ?? string.Empty}";
        }

        private string GetProfilerProjectBin()
        {
            return Path.Combine(
                GetSolutionDirectory(),
                "src",
                "Datadog.Trace.ClrProfiler.Native",
                "bin",
                EnvironmentTools.GetBuildConfiguration(),
                EnvironmentTools.GetPlatform().ToLower());
        }

        private string GetExecutingProjectBin()
        {
            return Path.GetDirectoryName(ExecutingAssembly.Location);
        }
    }
}
