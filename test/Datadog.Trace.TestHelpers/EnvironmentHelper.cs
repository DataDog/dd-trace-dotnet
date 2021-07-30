// <copyright file="EnvironmentHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Datadog.Trace.Configuration;
using Xunit.Abstractions;

namespace Datadog.Trace.TestHelpers
{
    public class EnvironmentHelper
    {
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
        private readonly TargetFrameworkAttribute _targetFramework;

        private bool _requiresProfiling;
        private string _integrationsFileLocation;
        private string _profilerFileLocation;

        public EnvironmentHelper(
            string sampleName,
            Type anchorType,
            ITestOutputHelper output,
            string samplesDirectory = null,
            bool prependSamplesToAppName = true,
            bool requiresProfiling = true)
        {
            SampleName = sampleName;
            _samplesDirectory = samplesDirectory ?? Path.Combine("test", "test-applications", "integrations");
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

        public static bool IsNet5()
        {
            return Environment.Version.Major >= 5;
        }

        public static bool IsCoreClr()
        {
            return RuntimeFrameworkDescription.Contains("core") || IsNet5();
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

        public static IEnumerable<string> GetProfilerPathCandidates(string sampleApplicationOutputDirectory)
        {
            string extension = EnvironmentTools.GetOS() switch
            {
                "win" => "dll",
                "linux" => "so",
                "osx" => "dylib",
                _ => throw new PlatformNotSupportedException()
            };

            string fileName = $"Datadog.Trace.ClrProfiler.Native.{extension}";

            var relativePath = Path.Combine("profiler-lib", fileName);

            if (sampleApplicationOutputDirectory != null)
            {
                yield return Path.Combine(sampleApplicationOutputDirectory, relativePath);
            }

            yield return Path.Combine(GetExecutingProjectBin(), relativePath);
            yield return Path.Combine(GetProfilerProjectBin(), fileName);
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
                "DD_TAGS",
                "DD_APPSEC_ENABLED",
                "DD_TRACE_CALLTARGET_ENABLED"
            };

            foreach (string variable in environmentVariables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }

        public void SetEnvironmentVariables(
            int agentPort,
            int aspNetCorePort,
            int? statsdPort,
            StringDictionary environmentVariables,
            string processToProfile = null,
            bool enableSecurity = false,
            bool callTargetEnabled = false)
        {
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
            }

            if (DebugModeEnabled)
            {
                environmentVariables["DD_TRACE_DEBUG"] = "1";
            }

            if (callTargetEnabled)
            {
                environmentVariables["DD_TRACE_CALLTARGET_ENABLED"] = "1";
            }

            if (!string.IsNullOrEmpty(processToProfile))
            {
                environmentVariables["DD_PROFILER_PROCESSES"] = Path.GetFileName(processToProfile);
            }

            string integrations = string.Join(";", GetIntegrationsFilePaths());
            environmentVariables["DD_INTEGRATIONS"] = integrations;
            environmentVariables["DD_TRACE_AGENT_HOSTNAME"] = "127.0.0.1";
            environmentVariables["DD_TRACE_AGENT_PORT"] = agentPort.ToString();

            // for ASP.NET Core sample apps, set the server's port
            environmentVariables["ASPNETCORE_URLS"] = $"http://127.0.0.1:{aspNetCorePort}/";

            if (statsdPort != null)
            {
                environmentVariables["DD_DOGSTATSD_PORT"] = statsdPort.Value.ToString();
            }

            if (enableSecurity)
            {
                environmentVariables[ConfigurationKeys.AppSecEnabled] = enableSecurity.ToString();
                // our tests require blocking to be activated to work, might add more fine grained control later
                environmentVariables[ConfigurationKeys.AppSecBlockingEnabled] = enableSecurity.ToString();
            }

            foreach (var name in new[] { "SERVICESTACK_REDIS_HOST", "STACKEXCHANGE_REDIS_HOST" })
            {
                var value = Environment.GetEnvironmentVariable(name);
                if (!string.IsNullOrEmpty(value))
                {
                    environmentVariables[name] = value;
                }
            }

            // set consistent env name (can be overwritten by custom environment variable)
            environmentVariables["DD_ENV"] = "integration_tests";

            // Don't attach the profiler to these processes
            environmentVariables["DD_PROFILER_EXCLUDE_PROCESSES"] =
                "devenv.exe;Microsoft.ServiceHub.Controller.exe;ServiceHub.Host.CLR.exe;ServiceHub.TestWindowStoreHost.exe;" +
                "ServiceHub.DataWarehouseHost.exe;sqlservr.exe;VBCSCompiler.exe;iisexpresstray.exe;msvsmon.exe;PerfWatson2.exe;" +
                "ServiceHub.IdentityHost.exe;ServiceHub.VSDetouredHost.exe;ServiceHub.SettingsHost.exe;ServiceHub.Host.CLR.x86.exe;" +
                "ServiceHub.RoslynCodeAnalysisService32.exe;MSBuild.exe;ServiceHub.ThreadedWaitDialog.exe";

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
                        EnvironmentTools.GetSolutionDirectory(),
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
                var paths = GetProfilerPathCandidates(GetSampleApplicationOutputDirectory()).ToArray();

                foreach (var candidate in paths)
                {
                    if (File.Exists(candidate))
                    {
                        _profilerFileLocation = candidate;
                        _output?.WriteLine($"Found profiler at {_profilerFileLocation}.");
                        return candidate;
                    }
                }

                throw new Exception($"Unable to find profiler in any of the paths: {string.Join("; ", paths)}");
            }

            return _profilerFileLocation;
        }

        public string GetSampleApplicationPath(string packageVersion = "", string framework = "")
        {
            string extension = "exe";

            if (IsCoreClr() || _samplesDirectory.Contains("aspnet"))
            {
                extension = "dll";
            }

            var appFileName = $"{FullSampleName}.{extension}";
            var sampleAppPath = Path.Combine(GetSampleApplicationOutputDirectory(packageVersion: packageVersion, framework: framework), appFileName);
            return sampleAppPath;
        }

        public string GetTestCommandForSampleApplicationPath(string packageVersion = "", string framework = "")
        {
            var appFileName = $"{FullSampleName}.dll";
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

        public string GetDotNetTest()
        {
            if (EnvironmentTools.IsWindows())
            {
                if (!IsCoreClr())
                {
                    string filePattern = @"C:\Program Files (x86)\Microsoft Visual Studio\{0}\{1}\Common7\IDE\CommonExtensions\Microsoft\TestWindow\vstest.console.exe";
                    List<Tuple<string, string>> lstTuple = new List<Tuple<string, string>>
                    {
                        Tuple.Create("2019", "Enterprise"),
                        Tuple.Create("2019", "Professional"),
                        Tuple.Create("2019", "Community"),
                        Tuple.Create("2017", "Enterprise"),
                        Tuple.Create("2017", "Professional"),
                        Tuple.Create("2017", "Community"),
                    };

                    foreach (Tuple<string, string> tuple in lstTuple)
                    {
                        var tryPath = string.Format(filePattern, tuple.Item1, tuple.Item2);
                        if (File.Exists(tryPath))
                        {
                            return tryPath;
                        }
                    }
                }

                return "dotnet.exe";
            }

            return "dotnet";
        }

        public string GetSampleProjectDirectory()
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
            var projectDir = Path.Combine(
                solutionDirectory,
                _samplesDirectory,
                $"{FullSampleName}");
            return projectDir;
        }

        public string GetSampleApplicationOutputDirectory(string packageVersion = "", string framework = "", bool usePublishFolder = true)
        {
            var targetFramework = string.IsNullOrEmpty(framework) ? GetTargetFramework() : framework;
            var binDir = Path.Combine(
                GetSampleProjectDirectory(),
                "bin");

            string outputDir;

            if (_samplesDirectory.Contains("aspnet"))
            {
                outputDir = Path.Combine(
                    binDir,
                    EnvironmentTools.GetBuildConfiguration(),
                    "publish");
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
            else if (usePublishFolder)
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework,
                    "publish");
            }
            else
            {
                outputDir = Path.Combine(
                    binDir,
                    packageVersion,
                    EnvironmentTools.GetBuildConfiguration(),
                    targetFramework);
            }

            return outputDir;
        }

        public string GetTargetFramework()
        {
            if (_isCoreClr)
            {
                if (_major >= 5)
                {
                    return $"net{_major}.{_minor}";
                }

                return $"netcoreapp{_major}.{_minor}";
            }

            return $"net{_major}{_minor}{_patch ?? string.Empty}";
        }

        private static string GetProfilerProjectBin()
        {
            return Path.Combine(
                EnvironmentTools.GetSolutionDirectory(),
                "src",
                "Datadog.Trace.ClrProfiler.Native",
                "bin",
                EnvironmentTools.GetBuildConfiguration(),
                EnvironmentTools.GetPlatform().ToLower());
        }

        private static string GetExecutingProjectBin()
        {
            return Path.GetDirectoryName(ExecutingAssembly.Location);
        }
    }
}
