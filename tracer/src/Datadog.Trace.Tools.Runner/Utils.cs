// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Tools.Runner.Gac;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class Utils
    {
        public const string Profilerid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        private const string CacheIntegrityFileName = ".dd-trace-runner-cache.integrity";
        private const string CacheMarkerFileName = ".dd-trace-runner-cache";
        private const string CacheIntegrityManifestVersion = "v2";
        private const string CacheLockFileExtension = ".lock";
        private const string CacheStagingDirectorySuffix = ".tmp.";
        private const int CacheKeyLength = 64;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Utils));

        public static string GetHomePath(string runnerFolder)
        {
            // In the current nuspec structure RunnerFolder has the following format:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp3.1\any
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp2.1\any
            // And the Home folder is:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\home
            // So we have to go up 3 folders.

            return DirectoryExists("Home", Path.Combine(runnerFolder, "..", "..", "..", "home"), Path.Combine(runnerFolder, "home"));
        }

        public static string GetDdDotnetPath(ApplicationContext applicationContext)
        {
            var tracerHome = GetHomePath(applicationContext.RunnerFolder);

            if (tracerHome == null)
            {
                return null;
            }

            // pick the right one depending on the platform
            var ddDotnet = (platform: applicationContext.Platform, arch: RuntimeInformation.OSArchitecture, musl: IsAlpine()) switch
            {
                (Platform.Windows, Architecture.X64, _) => Path.Combine(tracerHome, "win-x64", "dd-dotnet.exe"),
                (Platform.Windows, Architecture.X86, _) => Path.Combine(tracerHome, "win-x64", "dd-dotnet.exe"),
                (Platform.Linux, Architecture.X64, false) => Path.Combine(tracerHome, "linux-x64", "dd-dotnet"),
                (Platform.Linux, Architecture.X64, true) => Path.Combine(tracerHome, "linux-musl-x64", "dd-dotnet"),
                (Platform.Linux, Architecture.Arm64, false) => Path.Combine(tracerHome, "linux-arm64", "dd-dotnet"),
                (Platform.Linux, Architecture.Arm64, true) => Path.Combine(tracerHome, "linux-musl-arm64", "dd-dotnet"),
                var other => throw new NotSupportedException(
                    $"Unsupported platform/architecture combination: ({other.platform}{(other.musl ? " musl" : string.Empty)}/{other.arch})")
            };

            return ddDotnet;
        }

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(InvocationContext context, string runnerFolder, Platform platform, CommonTracerSettings options, CIVisibilityOptions ciVisibilityOptions)
        {
            var tracerHomeFolder = options.TracerHome.GetValue(context);

            var envVars = GetBaseProfilerEnvironmentVariables(runnerFolder, platform, tracerHomeFolder, ciVisibilityOptions);

            var environment = options.Environment.GetValue(context);

            if (!string.IsNullOrWhiteSpace(environment))
            {
                envVars[ConfigurationKeys.Environment] = environment;
            }

            var service = options.Service.GetValue(context);

            if (!string.IsNullOrWhiteSpace(service))
            {
                envVars[ConfigurationKeys.ServiceName] = service;
            }

            var version = options.Version.GetValue(context);

            if (!string.IsNullOrWhiteSpace(version))
            {
                envVars[ConfigurationKeys.ServiceVersion] = version;
            }

            var agentUrl = options.AgentUrl.GetValue(context);

            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                envVars[ConfigurationKeys.AgentUri] = agentUrl;
            }

            return envVars;
        }

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(InvocationContext context, string runnerFolder, Platform platform, LegacySettings options, CIVisibilityOptions ciVisibilityOptions)
        {
            var envVars = GetBaseProfilerEnvironmentVariables(runnerFolder, platform, options.TracerHomeFolderOption.GetValue(context), ciVisibilityOptions);

            var environment = options.EnvironmentOption.GetValue(context);

            if (!string.IsNullOrWhiteSpace(environment))
            {
                envVars["DD_ENV"] = environment;
            }

            var service = options.ServiceOption.GetValue(context);

            if (!string.IsNullOrWhiteSpace(service))
            {
                envVars["DD_SERVICE"] = service;
            }

            var version = options.VersionOption.GetValue(context);

            if (!string.IsNullOrWhiteSpace(version))
            {
                envVars["DD_VERSION"] = version;
            }

            var agentUrl = options.AgentUrlOption.GetValue(context);

            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                envVars["DD_TRACE_AGENT_URL"] = agentUrl;
            }

            var environmentValues = options.EnvironmentValuesOption.GetValue(context);

            if (!string.IsNullOrWhiteSpace(environmentValues))
            {
                foreach (var keyValue in environmentValues.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (!string.IsNullOrWhiteSpace(keyValue?.Trim()))
                    {
                        var kvArray = keyValue.Split('=');
                        if (kvArray.Length == 2)
                        {
                            envVars[kvArray[0]] = kvArray[1];
                        }
                    }
                }
            }

            return envVars;
        }

        public static void SetCommonTracerSettingsToCurrentProcess(InvocationContext context, CommonTracerSettings options)
        {
            var environment = options.Environment.GetValue(context);

            // Settings back DD_ENV to use it in the current process (eg for TestOptimization's TestSession)
            if (!string.IsNullOrWhiteSpace(environment))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Environment, environment);
            }

            var service = options.Service.GetValue(context);

            // Settings back DD_SERVICE to use it in the current process (eg for TestOptimization's TestSession)
            if (!string.IsNullOrWhiteSpace(service))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ServiceName, service);
            }

            var version = options.Version.GetValue(context);

            // Settings back DD_VERSION to use it in the current process (eg for TestOptimization's TestSession)
            if (!string.IsNullOrWhiteSpace(version))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ServiceVersion, version);
            }

            var agentUrl = options.AgentUrl.GetValue(context);

            // Settings back DD_TRACE_AGENT_URL to use it in the current process (eg for TestOptimization's TestSession)
            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.AgentUri, agentUrl);
            }
        }

        public static string DirectoryExists(string name, params string[] paths)
        {
            string folderName = null;

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    var tmpFolder = Path.GetFullPath(paths[i]);
                    if (Directory.Exists(tmpFolder))
                    {
                        folderName = tmpFolder;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error: The '{name}' directory check thrown an exception: {ex}");
            }

            return folderName;
        }

        public static string FileExists(string filePath)
        {
            try
            {
                filePath = Path.GetFullPath(filePath);
                if (!File.Exists(filePath))
                {
                    WriteError($"Error: The file '{filePath}' can't be found.");
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error: The file '{filePath}' check thrown an exception: {ex}");
            }

            return filePath;
        }

        public static string FileExistsOrNull(string filePath)
        {
            try
            {
                filePath = Path.GetFullPath(filePath);
                if (!File.Exists(filePath))
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error: The file '{filePath}' check thrown an exception: {ex}");
            }

            return filePath;
        }

        public static ProcessStartInfo GetProcessStartInfo(string filename, string currentDirectory, IDictionary<string, string> environmentVariables)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(filename)
            {
                UseShellExecute = false,
                WorkingDirectory = currentDirectory,
            };

            IDictionary currentEnvVars = Environment.GetEnvironmentVariables();
            if (currentEnvVars != null)
            {
                foreach (DictionaryEntry item in currentEnvVars)
                {
                    processInfo.Environment[item.Key.ToString()] = item.Value.ToString();
                }
            }

            if (environmentVariables != null)
            {
                foreach (KeyValuePair<string, string> item in environmentVariables)
                {
                    processInfo.Environment[item.Key] = item.Value;
                }
            }

            return processInfo;
        }

        public static int RunProcess(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            try
            {
                using var childProcess = new Process();
                childProcess.StartInfo = startInfo;
                childProcess.EnableRaisingEvents = true;
                childProcess.Start();

                using var ctr = cancellationToken.Register(
                    () =>
                    {
                        try
                        {
                            childProcess.Kill();
                        }
                        catch
                        {
                            // .
                        }
                    });

                childProcess.WaitForExit();
                return cancellationToken.IsCancellationRequested ? 1 : childProcess.ExitCode;
            }
            catch (System.ComponentModel.Win32Exception win32Exception)
            {
                // https://github.com/dotnet/runtime/blob/d099f075e45d2aa6007a22b71b45a08758559f80/src/libraries/System.Diagnostics.Process/src/System/Diagnostics/Process.cs#L1750-L1755
                // The file could not be found, let's try to find it using the where command and retry
                if (!File.Exists(startInfo.FileName))
                {
                    if (FrameworkDescription.Instance.OSDescription == OSPlatformName.Linux)
                    {
                        // In linux we need to use `whereis`
                        // output example:
                        // dotnet: /usr/bin/dotnet /usr/lib/dotnet /etc/dotnet
                        var cmdResponse = ProcessHelpers.RunCommand(new ProcessHelpers.Command("whereis", $"-b {startInfo.FileName}"));
                        if (cmdResponse?.ExitCode == 0 &&
                            cmdResponse.Output.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } outputLines &&
                            outputLines[0] is { Length: > 0 } temporalOutput)
                        {
                            foreach (var path in ProcessHelpers.ParseWhereisOutput(temporalOutput))
                            {
                                if (File.Exists(path))
                                {
                                    startInfo.FileName = path;
                                    return RunProcess(startInfo, cancellationToken);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Both windows and macos can use `where` instead
                        var cmdResponse = ProcessHelpers.RunCommand(new ProcessHelpers.Command("where", startInfo.FileName));
                        if (cmdResponse?.ExitCode == 0 &&
                            cmdResponse.Output.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } outputLines &&
                            outputLines[0] is { Length: > 0 } processPath &&
                            File.Exists(processPath))
                        {
                            startInfo.FileName = processPath;
                            return RunProcess(startInfo, cancellationToken);
                        }
                    }
                }

                AnsiConsole.WriteException(win32Exception);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
            }

            return 1;
        }

        public static string[] SplitArgs(string command, bool keepQuote = false)
        {
            if (string.IsNullOrEmpty(command))
            {
                return new string[0];
            }

            var inQuote = false;
            var chars = command.ToCharArray().Select(v =>
            {
                if (v == '"')
                {
                    inQuote = !inQuote;
                }

                return !inQuote && v == ' ' ? '\n' : v;
            }).ToArray();

            return new string(chars).Split('\n')
                .Select(x => keepQuote ? x : x.Trim('"'))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }

        public static string GetEnvironmentVariable(string key, string defaultValue = null)
        {
            try
            {
                return Environment.GetEnvironmentVariable(key);
            }
            catch (Exception ex)
            {
                WriteError($"Error while reading environment variable {key}: {ex}");
            }

            return defaultValue;
        }

        public static async Task<AgentConfiguration> CheckAgentConnectionAsync(string agentUrl)
        {
            var (configuration, discoveryService) = await GetDiscoveryServiceAndCheckConnectionAsync(agentUrl).ConfigureAwait(false);
            await discoveryService.DisposeAsync().ConfigureAwait(false);
            return configuration;
        }

        public static async Task<(AgentConfiguration Configuration, DiscoveryService DiscoveryService)> GetDiscoveryServiceAndCheckConnectionAsync(string agentUrl)
        {
            var env = new NameValueCollection();
            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                env[ConfigurationKeys.AgentUri] = agentUrl;
            }

            var configurationSource = new CompositeConfigurationSource();
            configurationSource.Add(new NameValueConfigurationSource(env, ConfigurationOrigins.EnvVars));
            configurationSource.Add(GlobalConfigurationSource.Instance);

            var settings = new TracerSettings(configurationSource, new ConfigurationTelemetry(), new OverrideErrorLog());

            Log.Debug("Creating DiscoveryService for: {AgentUri}", settings.Manager.InitialExporterSettings.AgentUri);
            var discoveryService = DiscoveryService.CreateUnmanaged(
                settings.Manager.InitialExporterSettings,
                ContainerMetadata.Instance,
                new ServiceRemappingHash(null),
                tcpTimeout: TimeSpan.FromSeconds(5),
                initialRetryDelayMs: 200,
                maxRetryDelayMs: 1000,
                recheckIntervalMs: int.MaxValue);

            var tcs = new TaskCompletionSource<AgentConfiguration>(TaskCreationOptions.RunContinuationsAsynchronously);
            discoveryService.SubscribeToChanges(aCfg => tcs.TrySetResult(aCfg));

            var cts = new CancellationTokenSource();
            cts.CancelAfter(5000);
            using (cts.Token.Register(
                       () =>
                       {
                           WriteError($"Error connecting to the Datadog Agent at {settings.Manager.InitialExporterSettings.AgentUri}.");
                           tcs.TrySetResult(null);
                       }))
            {
                var configuration = await tcs.Task.ConfigureAwait(false);
                return (configuration, discoveryService);
            }
        }

        internal static void WriteError(string message)
        {
            AnsiConsole.MarkupLine($"[red] [[FAILURE]]: {message.EscapeMarkup()}[/]");
        }

        internal static void WriteWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow] [[WARNING]]: {message.EscapeMarkup()}[/]");
        }

        internal static void WriteSuccess(string message)
        {
            AnsiConsole.MarkupLine($"[lime] [[SUCCESS]]: {message.EscapeMarkup()}[/]");
        }

        internal static void WriteInfo(string message)
        {
            AnsiConsole.MarkupLine($"[aqua] [[INFO]]: {message.EscapeMarkup()}[/]");
        }

        internal static bool IsAlpine()
        {
            try
            {
                if (File.Exists("/etc/os-release"))
                {
                    var strArray = File.ReadAllLines("/etc/os-release");
                    foreach (var str in strArray)
                    {
                        if (str.StartsWith("ID=", StringComparison.Ordinal))
                        {
                            return str.Substring(3).Trim('"', '\'') == "alpine";
                        }
                    }
                }
            }
            catch
            {
                // ignore error checking if the file doesn't exist or we can't read it
            }

            return false;
        }

        private static Dictionary<string, string> GetBaseProfilerEnvironmentVariables(string runnerFolder, Platform platform, string tracerHomeFolder, CIVisibilityOptions ciVisibilityOptions = null)
        {
            string tracerHome = null;
            if (!string.IsNullOrEmpty(tracerHomeFolder))
            {
                tracerHome = Path.GetFullPath(tracerHomeFolder);
                if (!Directory.Exists(tracerHome))
                {
                    WriteError("Error: The specified home folder doesn't exist.");
                }
            }

            tracerHome ??= GetHomePath(runnerFolder);

            if (tracerHome == null)
            {
                WriteError("Error: The home directory can't be found. Check that the tool is correctly installed, or use --tracer-home to set a custom path.");
                return null;
            }

            if (ciVisibilityOptions?.ReducePathLength == true)
            {
                // Due to:
                // https://developercommunity.visualstudio.com/t/vsotasksetvariable-contains-logging-command-keywor/1249340#T-N1253996
                // ReducePathLength used to copy into a fixed temp directory. Keep the same path-shortening behavior,
                // but only through a validated user-local cache so a co-tenant cannot pre-create the destination.
                string cachedTracerHome = null;
                try
                {
                    var cacheRoot = GetTracerHomeCacheRoot();
                    if (Path.Combine(cacheRoot, new string('0', CacheKeyLength)).Length < tracerHome.Length)
                    {
                        var cacheKey = GetTracerHomeCacheKey(tracerHome);
                        cachedTracerHome = Path.Combine(cacheRoot, cacheKey);
                        EnsureCachedTracerHome(tracerHome, cachedTracerHome, cacheKey);
                        tracerHome = cachedTracerHome;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Unable to copy tracer home to a shorter temporary path.");
                    if (cachedTracerHome is not null)
                    {
                        TryDeleteDirectory(cachedTracerHome);
                    }
                }
            }

            string tracerMsBuild = FileExists(Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.MSBuild.dll"));
            string tracerProfiler32 = string.Empty;
            string tracerProfiler64 = string.Empty;
            string tracerProfilerArm64 = null;
            string ldPreload = string.Empty;
            string devPath = string.Empty;

            if (platform == Platform.Windows)
            {
                tracerProfiler32 = FileExists(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
                tracerProfiler64 = FileExists(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
                tracerProfilerArm64 = FileExistsOrNull(Path.Combine(tracerHome, "win-ARM64EC", "Datadog.Trace.ClrProfiler.Native.dll"));

                if (ciVisibilityOptions is not null)
                {
                    // For full compatibility with .NET Framework we need to either install Datadog.Trace.dll to the GAC or enable Development mode for vstest.console
                    // This is a best effort approach by:
                    //   1. Check if `Datadog.Trace` is installed in the gac using `gacutil`, if not, we try to install it.
                    //   2. If that doesn't work we try to locate `vstest.console.exe.config` to enable debug mode on this
                    //      command so we can inject the DevPath environment variable
                    var installedInGac = false;
                    if (ciVisibilityOptions.EnableGacInstallation)
                    {
                        installedInGac = EnsureDatadogTraceIsInTheGac(tracerHome, platform);
                    }

                    if (!installedInGac && ciVisibilityOptions.EnableVsTestConsoleConfigModification)
                    {
                        if (EnsureNETFrameworkVSTestConsoleDevPathSupport())
                        {
                            devPath = Path.Combine(tracerHome, "net461");
                        }
                    }
                }
            }
            else if (platform == Platform.Linux)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                {
                    var archFolder = IsAlpine() ? "linux-musl-x64" : "linux-x64";
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Trace.ClrProfiler.Native.so"));
                    ldPreload = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Linux.ApiWrapper.x64.so"));
                }
                else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    var archFolder = IsAlpine() ? "linux-musl-arm64" : "linux-arm64";
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Trace.ClrProfiler.Native.so"));
                    tracerProfilerArm64 = tracerProfiler64;
                    ldPreload = FileExists(Path.Combine(tracerHome, archFolder, "Datadog.Linux.ApiWrapper.x64.so"));
                }
                else
                {
                    WriteError($"Error: Linux {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
            }
            else if (platform == Platform.MacOS)
            {
                tracerProfiler64 = FileExists(Path.Combine(tracerHome, "osx", "Datadog.Trace.ClrProfiler.Native.dylib"));
                tracerProfilerArm64 = tracerProfiler64;
            }

            var envVars = new Dictionary<string, string>
            {
                ["DD_DOTNET_TRACER_HOME"] = tracerHome,
                ["DD_DOTNET_TRACER_MSBUILD"] = tracerMsBuild,
                ["CORECLR_ENABLE_PROFILING"] = "1",
                ["CORECLR_PROFILER"] = Profilerid,
                ["CORECLR_PROFILER_PATH_32"] = tracerProfiler32,
                ["CORECLR_PROFILER_PATH_64"] = tracerProfiler64,
                ["COR_ENABLE_PROFILING"] = "1",
                ["COR_PROFILER"] = Profilerid,
                ["COR_PROFILER_PATH_32"] = tracerProfiler32,
                ["COR_PROFILER_PATH_64"] = tracerProfiler64,
                // Preventively set EnableDiagnostics to override any ambient value
                ["COMPlus_EnableDiagnostics"] = "1",
                ["DOTNET_EnableDiagnostics"] = "1",
                ["DOTNET_EnableDiagnostics_Profiler"] = "1",
                ["COMPlus_EnableDiagnostics_Profiler"] = "1",
            };

            if (!string.IsNullOrEmpty(ldPreload))
            {
                envVars["LD_PRELOAD"] = ldPreload;
            }

            if (!string.IsNullOrEmpty(tracerProfilerArm64))
            {
                envVars["CORECLR_PROFILER_PATH_ARM64"] = tracerProfilerArm64;
                envVars["COR_PROFILER_PATH_ARM64"] = tracerProfilerArm64;
            }

            const string installTypeKey = "DD_INSTRUMENTATION_INSTALL_TYPE";
            if (string.IsNullOrEmpty(GetEnvironmentVariable(installTypeKey)))
            {
                envVars[installTypeKey] = "dd_trace_tool";
            }

            if (!string.IsNullOrEmpty(devPath))
            {
                envVars["DEVPATH"] = devPath;
            }

            return envVars;
        }

        private static bool EnsureDatadogTraceIsInTheGac(string tracerHome, Platform platform)
        {
            var datadogTraceDllPath = FileExistsOrNull(Path.Combine(tracerHome, "net461", "Datadog.Trace.dll"));

            try
            {
                // Let's try to execute the built-in GAC installer to avoid the gacutil dependency
                if (platform == Platform.Windows && datadogTraceDllPath is not null)
                {
#pragma warning disable CA1416
                    const string datadogTraceAssemblyName = "Datadog.Trace";
                    var datadogTraceAssemblyNameInfo = AssemblyName.GetAssemblyName(datadogTraceDllPath);
                    using var gacMethods = Gac.GacNativeMethods.Create();
                    var assemblyCache = gacMethods.CreateAssemblyCache();
                    var asmInfo = new Gac.AssemblyInfo();
                    var hr = assemblyCache.QueryAssemblyInfo(Gac.QueryAssemblyInfoFlag.QUERYASMINFO_FLAG_GETSIZE, datadogTraceAssemblyName, ref asmInfo);
                    if (hr == 0 && asmInfo.AssemblyFlags == Gac.AssemblyInfoFlags.ASSEMBLYINFO_FLAG_INSTALLED)
                    {
                        try
                        {
                            // Datadog.Trace is in the GAC, let's get the version
                            var installedAssemblyNames = gacMethods.GetAssemblyNames(datadogTraceAssemblyName);
                            var hasSameVersionInstalled = false;
                            foreach (var installedAssemblyName in installedAssemblyNames)
                            {
                                Log.Information("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace version {Version} installed.", installedAssemblyName.Version);
                                hasSameVersionInstalled |= installedAssemblyName.Version == datadogTraceAssemblyNameInfo.Version;
                            }

                            if (hasSameVersionInstalled)
                            {
                                // the same version of Datadog.Trace is in the GAC, do nothing
                                Log.Information("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace version {Version} is already installed in the gac.", datadogTraceAssemblyNameInfo.Version);
                                return true;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Error getting the installed assembly names.");
                        }
                    }

                    Log.Warning("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace ({Version}) is not in the GAC, let's try to install it.", datadogTraceAssemblyNameInfo.Version);

                    if (Gac.AdministratorHelper.IsElevated)
                    {
                        WriteInfo("Datadog.Trace is not installed in the GAC, installing it...");

                        hr = assemblyCache.InstallAssembly(AssemblyCacheInstallFlags.IASSEMBLYCACHE_INSTALL_FLAG_FORCE_REFRESH, datadogTraceDllPath, IntPtr.Zero);
                        if (hr == 0)
                        {
                            Log.Information("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace was installed in the gac.");
                            WriteSuccess($"Assembly '{datadogTraceDllPath}' was installed in the GAC successfully.");
                            return true;
                        }
                    }
                    else
                    {
                        WriteInfo("Datadog.Trace is not installed in the GAC, the installation will require Administrator permissions. Installing...");

#if NET6_0_OR_GREATER
                        var processPath = Environment.ProcessPath ?? Environment.GetCommandLineArgs()[0];
#else
                        var processPath = Environment.GetCommandLineArgs()[0];
#endif
                        // If we get the .dll filepath we change it to the .exe one
                        if (string.Equals(Path.GetExtension(processPath), ".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            processPath = Path.ChangeExtension(processPath, ".exe");
                        }

                        if (ProcessHelpers.RunCommand(new ProcessHelpers.Command(processPath, $"gac install {datadogTraceDllPath}", verb: "runas")) is { } cmdGacInstallResponse &&
                            cmdGacInstallResponse.ExitCode == 0)
                        {
                            // dd-trace gac install was successful.
                            Log.Information("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace was installed in the gac.");
                            WriteSuccess("Datadog.Trace was installed in the GAC.");
                            return true;
                        }
                    }
#pragma warning restore CA1416
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error using the built-in gac installer.");
            }

            // We try to ensure Datadog.Trace.dll is installed in the gac for compatibility with .NET Framework fusion class loader
            // Let's find gacutil, because CI Visibility runs with the SDK / CI environments it's probable that's available.
            // Because gacutil is only available in Windows we use `where` command to find it.
            var cmdResponse = ProcessHelpers.RunCommand(new ProcessHelpers.Command("where", "gacutil"));
            if (cmdResponse?.ExitCode == 0 &&
                cmdResponse.Output.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } outputLines &&
                outputLines[0] is { Length: > 0 } gacPath)
            {
                Log.Debug("EnsureDatadogTraceIsInTheGac: gacutil was found.");
                var cmdGacListResponse = ProcessHelpers.RunCommand(new ProcessHelpers.Command(gacPath, "/l"));
                if (cmdGacListResponse?.ExitCode == 0)
                {
                    if (cmdGacListResponse.Output?.Contains(typeof(TracerConstants).Assembly.FullName!, StringComparison.OrdinalIgnoreCase) != true)
                    {
                        // Datadog.Trace is not in the GAC, let's try to install it.
                        // We run the gacutil /i command using runas verb to elevate privileges
                        Log.Warning("EnsureDatadogTraceIsInTheGac: Datadog.Trace is not in the GAC, let's try to install it.");
                        WriteInfo("Datadog.Trace is not installed in the GAC, the installation will require Administrator permissions. Installing...");
                        if (datadogTraceDllPath is not null &&
                            ProcessHelpers.RunCommand(new ProcessHelpers.Command(gacPath, $"/if {datadogTraceDllPath}", verb: "runas")) is { } cmdGacInstallResponse)
                        {
                            if (cmdGacInstallResponse.ExitCode == 0)
                            {
                                // gacutil install was successful.
                                Log.Information("EnsureDatadogTraceIsInTheGac: Datadog.Trace was installed in the gac.");
                                WriteSuccess("Datadog.Trace was installed in the GAC.");
                                return true;
                            }

                            Log.Warning("EnsureDatadogTraceIsInTheGac: gacutil returned an error, Datadog.Trace was not installed in the gac.");
                            WriteWarning("Datadog.Trace was not installed in the GAC.");
                            return false;
                        }
                    }
                    else
                    {
                        // Datadog.Trace is in the GAC, do nothing
                        Log.Information("EnsureDatadogTraceIsInTheGac: Datadog.Trace is already installed in the gac.");
                        return true;
                    }
                }
                else
                {
                    // `gacutil /l` failed
                    Log.Warning("EnsureDatadogTraceIsInTheGac: Call to `gacutil /l` failed.");
                }
            }
            else
            {
                // `gacutil` cannot be found
                Log.Warning("EnsureDatadogTraceIsInTheGac: gacutil cannot be found.");
            }

            return false;
        }

        private static bool EnsureNETFrameworkVSTestConsoleDevPathSupport()
        {
            // As a final workaround for .NET Framework compatibility we can use the DevPath approach.
            // https://learn.microsoft.com/en-us/dotnet/framework/configure-apps/how-to-locate-assemblies-by-using-devpath
            // .NET Framework tests runs using `vstest.console.exe` console app. So we need to locate this file and modify
            // the configuration `vstest.console.exe.config` file to add:
            /*
                <configuration>
                  <runtime>
                    <developmentMode developerInstallation="true"/>
                  </runtime>
                </configuration>
             */

            Log.Debug("EnsureNETFrameworkVSTestConsoleDevPathSupport: Looking for vstest.console");
            // Because vstest.console is only available in windows we use `where` command to find it.
            var cmdResponse = ProcessHelpers.RunCommand(new ProcessHelpers.Command("where", "vstest.console"));
            if (cmdResponse?.ExitCode == 0 &&
                cmdResponse.Output.Split(["\n", "\r\n"], StringSplitOptions.RemoveEmptyEntries) is { Length: > 0 } outputLines &&
                outputLines[0] is { Length: > 0 } vstestConsolePath)
            {
                Log.Debug("EnsureNETFrameworkVSTestConsoleDevPathSupport: vstest.console was found.");
                var configPath = $"{vstestConsolePath.Trim()}.config";
                if (File.Exists(configPath))
                {
                    try
                    {
                        Log.Debug("EnsureNETFrameworkVSTestConsoleDevPathSupport: vstest.console configuration file was found.");
                        var configContent = File.ReadAllText(configPath);
                        var xmlDocument = new XmlDocument();
                        xmlDocument.LoadXml(configContent);
                        var xmlDoc = xmlDocument.DocumentElement!;
                        var isDirty = false;
                        var runtimeNode = xmlDoc["runtime"];
                        if (runtimeNode is null)
                        {
                            runtimeNode = (XmlElement)xmlDoc.AppendChild(xmlDocument.CreateElement("runtime"))!;
                            isDirty = true;
                        }

                        var developmentModeNode = runtimeNode["developmentMode"];
                        if (developmentModeNode is null)
                        {
                            developmentModeNode = (XmlElement)runtimeNode.AppendChild(xmlDocument.CreateElement("developmentMode"))!;
                            isDirty = true;
                        }

                        var developerInstallationAttribute = developmentModeNode.Attributes["developerInstallation"];
                        if (developerInstallationAttribute is null)
                        {
                            developmentModeNode.SetAttribute("developerInstallation", "true");
                            isDirty = true;
                        }
                        else if (developerInstallationAttribute.Value != "true")
                        {
                            developerInstallationAttribute.Value = "true";
                            isDirty = true;
                        }

                        if (isDirty)
                        {
                            try
                            {
                                var outerXml = xmlDocument.OuterXml;
                                Log.Debug("EnsureNETFrameworkVSTestConsoleDevPathSupport: vstest.console configuration file content: {Content}", outerXml);
                                File.WriteAllText(configPath, outerXml);
                                Log.Information("EnsureNETFrameworkVSTestConsoleDevPathSupport: vstest.console configuration file has been configured as developer mode.");
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "EnsureNETFrameworkVSTestConsoleDevPathSupport: Error writing the vstest.console configuration file.");
                                return false;
                            }
                        }
                        else
                        {
                            Log.Information("EnsureNETFrameworkVSTestConsoleDevPathSupport: vstest.console configuration file was already configured as developer mode.");
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "EnsureNETFrameworkVSTestConsoleDevPathSupport: Error writing the vstest.console configuration file.");
                        return false;
                    }
                }

                Log.Warning("EnsureNETFrameworkVSTestConsoleDevPathSupport: vstest.console configuration file was not found.");
            }

            return false;
        }

        /// <summary>
        /// Convert the arguments array to a string
        /// </summary>
        /// <remarks>
        /// This code is taken from https://source.dot.net/#System.Private.CoreLib/src/libraries/System.Private.CoreLib/src/System/PasteArguments.cs,624678ba1465e776
        /// </remarks>
        /// <param name="args">Arguments array</param>
        /// <returns>String of arguments</returns>
        public static string GetArgumentsAsString(IEnumerable<string> args)
        {
            const char Quote = '\"';
            const char Backslash = '\\';
            var stringBuilder = StringBuilderCache.Acquire();

            foreach (var argument in args)
            {
                if (stringBuilder.Length != 0)
                {
                    stringBuilder.Append(' ');
                }

                // Parsing rules for non-argv[0] arguments:
                //   - Backslash is a normal character except followed by a quote.
                //   - 2N backslashes followed by a quote ==> N literal backslashes followed by unescaped quote
                //   - 2N+1 backslashes followed by a quote ==> N literal backslashes followed by a literal quote
                //   - Parsing stops at first whitespace outside of quoted region.
                //   - (post 2008 rule): A closing quote followed by another quote ==> literal quote, and parsing remains in quoting mode.
                if (argument.Length != 0 && ContainsNoWhitespaceOrQuotes(argument))
                {
                    // Simple case - no quoting or changes needed.
                    stringBuilder.Append(argument);
                }
                else
                {
                    stringBuilder.Append(Quote);
                    int idx = 0;
                    while (idx < argument.Length)
                    {
                        char c = argument[idx++];
                        if (c == Backslash)
                        {
                            int numBackSlash = 1;
                            while (idx < argument.Length && argument[idx] == Backslash)
                            {
                                idx++;
                                numBackSlash++;
                            }

                            if (idx == argument.Length)
                            {
                                // We'll emit an end quote after this so must double the number of backslashes.
                                stringBuilder.Append(Backslash, numBackSlash * 2);
                            }
                            else if (argument[idx] == Quote)
                            {
                                // Backslashes will be followed by a quote. Must double the number of backslashes.
                                stringBuilder.Append(Backslash, (numBackSlash * 2) + 1);
                                stringBuilder.Append(Quote);
                                idx++;
                            }
                            else
                            {
                                // Backslash will not be followed by a quote, so emit as normal characters.
                                stringBuilder.Append(Backslash, numBackSlash);
                            }

                            continue;
                        }

                        if (c == Quote)
                        {
                            // Escape the quote so it appears as a literal. This also guarantees that we won't end up generating a closing quote followed
                            // by another quote (which parses differently pre-2008 vs. post-2008.)
                            stringBuilder.Append(Backslash);
                            stringBuilder.Append(Quote);
                            continue;
                        }

                        stringBuilder.Append(c);
                    }

                    stringBuilder.Append(Quote);
                }
            }

            return StringBuilderCache.GetStringAndRelease(stringBuilder);

            static bool ContainsNoWhitespaceOrQuotes(string s)
            {
                for (int i = 0; i < s.Length; i++)
                {
                    char c = s[i];
                    if (char.IsWhiteSpace(c) || c == Quote)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            targetPath = Path.GetFullPath(targetPath);

            foreach (var entry in EnumerateTracerHomeEntries(sourcePath, ignoreRootCacheMetadata: false))
            {
                var targetEntryPath = GetPathFromRelativePath(targetPath, entry.RelativePath);
                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(targetEntryPath);
                    continue;
                }

                var parentDirectory = Path.GetDirectoryName(targetEntryPath);
                if (!string.IsNullOrEmpty(parentDirectory))
                {
                    Directory.CreateDirectory(parentDirectory);
                }

                File.Copy(entry.FullPath, targetEntryPath, overwrite: true);
            }
        }

        private static void EnsureCachedTracerHome(string tracerHome, string cachedTracerHome, string cacheKey)
        {
            // Build the manifest before taking the cache lock so every reuse decision is based on the source
            // tracer home observed by this run, not on metadata that may already exist in the cache.
            var integrityManifest = CreateCacheIntegrityManifest(tracerHome);
            var cacheParent = Path.GetDirectoryName(Path.GetFullPath(cachedTracerHome));
            if (string.IsNullOrEmpty(cacheParent))
            {
                throw new IOException($"Unable to locate parent directory for cached tracer home '{cachedTracerHome}'.");
            }

            // The parent is validated before the lock file is opened; otherwise the lock itself could be created
            // in a shared writable directory and used as an attacker-controlled synchronization point.
            CreatePrivateDirectory(cacheParent);
            using var cacheLock = AcquireCacheLock(cachedTracerHome);
            if (IsCachedTracerHomeReady(cachedTracerHome, cacheKey, integrityManifest))
            {
                return;
            }

            var stagingTracerHome = cachedTracerHome + CacheStagingDirectorySuffix + Guid.NewGuid().ToString("N");
            try
            {
                TryDeleteDirectory(stagingTracerHome);
                // Copy into a private staging directory and publish with a final rename. The child process only sees
                // cachedTracerHome after the copy, integrity validation, and marker write have all succeeded.
                CreatePrivateDirectory(stagingTracerHome);
                CopyFilesRecursively(tracerHome, stagingTracerHome);
                if (!ValidateCachedTracerHomeIntegrity(stagingTracerHome, integrityManifest))
                {
                    throw new IOException($"Cached tracer home '{stagingTracerHome}' failed integrity validation.");
                }

                File.WriteAllText(Path.Combine(stagingTracerHome, CacheIntegrityFileName), integrityManifest.Content);
                // The marker is written last so interrupted copies are not reused by later runs.
                File.WriteAllText(Path.Combine(stagingTracerHome, CacheMarkerFileName), cacheKey);

                TryDeleteDirectory(cachedTracerHome);
                if (Directory.Exists(cachedTracerHome))
                {
                    throw new IOException($"Unable to replace cached tracer home '{cachedTracerHome}'.");
                }

                Directory.Move(stagingTracerHome, cachedTracerHome);
            }
            finally
            {
                TryDeleteDirectory(stagingTracerHome);
            }
        }

        private static bool IsCachedTracerHomeReady(string cachedTracerHome, string cacheKey, CacheIntegrityManifest integrityManifest)
        {
            if (!Directory.Exists(cachedTracerHome))
            {
                return false;
            }

            ValidateExistingPrivateDirectory(cachedTracerHome);
            var markerPath = Path.Combine(cachedTracerHome, CacheMarkerFileName);
            var integrityPath = Path.Combine(cachedTracerHome, CacheIntegrityFileName);
            return FileContentEquals(markerPath, cacheKey) &&
                   FileContentEquals(integrityPath, integrityManifest.Content) &&
                   ValidateCachedTracerHomeIntegrity(cachedTracerHome, integrityManifest);
        }

        private static FileStream AcquireCacheLock(string cachedTracerHome)
        {
            var lockPath = cachedTracerHome + CacheLockFileExtension;
            if (File.Exists(lockPath) && !IsRegularFile(lockPath))
            {
                throw new IOException($"Cache lock path '{lockPath}' must be a regular file.");
            }

            return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }

        private static string GetTracerHomeCacheRoot()
        {
            // Keep the cache under a user-local root instead of shared temp to avoid cross-user path hijacking.
            var cacheRoot = Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LOCALAPPDATA" : "XDG_CACHE_HOME");
            if (string.IsNullOrEmpty(cacheRoot))
            {
                cacheRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            }

            if (string.IsNullOrEmpty(cacheRoot))
            {
                throw new InvalidOperationException("Unable to locate a user-local cache directory.");
            }

            return Path.Combine(cacheRoot, "Datadog", "dd-trace", "runner", "tracer-home");
        }

        private static string GetTracerHomeCacheKey(string tracerHome)
        {
            // Include source file metadata so changed tracer homes use a different cache directory.
            tracerHome = Path.GetFullPath(tracerHome);
            var builder = StringBuilderCache.Acquire();
            builder.Append(tracerHome);
            builder.Append('|');
            builder.Append(GetTracerHomeAssemblyVersion(tracerHome));
            builder.Append('|');

            var entries = new List<TracerHomeEntry>();
            foreach (var entry in EnumerateTracerHomeEntries(tracerHome, ignoreRootCacheMetadata: false))
            {
                entries.Add(entry);
            }

            entries.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal));
            foreach (var entry in entries)
            {
                builder.Append(entry.RelativePath);
                builder.Append('|');
                builder.Append(entry.IsDirectory ? 'd' : 'f');
                builder.Append('|');
                if (!entry.IsDirectory)
                {
                    var fileInfo = new FileInfo(entry.FullPath);
                    builder.Append(fileInfo.Length);
                    builder.Append('|');
                    builder.Append(fileInfo.LastWriteTimeUtc.Ticks);
                }

                builder.Append(';');
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(StringBuilderCache.GetStringAndRelease(builder)));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string GetTracerHomeAssemblyVersion(string tracerHome)
        {
            var tracerAssemblyPath = Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.dll");
            if (!File.Exists(tracerAssemblyPath))
            {
                return string.Empty;
            }

            try
            {
                return AssemblyName.GetAssemblyName(tracerAssemblyPath).Version?.ToString() ?? string.Empty;
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException or UnauthorizedAccessException)
            {
                Log.Debug(ex, "Unable to read Datadog.Trace.dll version from tracer home.");
                return string.Empty;
            }
        }

        private static CacheIntegrityManifest CreateCacheIntegrityManifest(string tracerHome)
        {
            // Build the expected manifest from the source tracer home on every run; a cached manifest is never trusted by itself.
            var entries = CreateCacheIntegrityEntries(tracerHome, ignoreRootCacheMetadata: false);

            var builder = StringBuilderCache.Acquire();
            builder.AppendLine(CacheIntegrityManifestVersion);
            foreach (var entry in entries)
            {
                builder.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.RelativePath)));
                builder.Append('|');
                builder.Append(entry.IsDirectory ? 'd' : 'f');
                builder.Append('|');
                builder.Append(entry.Length);
                builder.Append('|');
                builder.Append(entry.Sha256);
                builder.AppendLine();
            }

            return new CacheIntegrityManifest(entries, StringBuilderCache.GetStringAndRelease(builder));
        }

        private static CacheIntegrityEntry[] CreateCacheIntegrityEntries(string tracerHome, bool ignoreRootCacheMetadata)
        {
            var entries = new List<CacheIntegrityEntry>();
            foreach (var entry in EnumerateTracerHomeEntries(tracerHome, ignoreRootCacheMetadata))
            {
                if (entry.IsDirectory)
                {
                    entries.Add(new CacheIntegrityEntry(entry.RelativePath, true, 0, string.Empty));
                    continue;
                }

                var fileInfo = new FileInfo(entry.FullPath);
                entries.Add(new CacheIntegrityEntry(entry.RelativePath, false, fileInfo.Length, ComputeSha256(entry.FullPath)));
            }

            entries.Sort((left, right) => string.Compare(left.RelativePath, right.RelativePath, StringComparison.Ordinal));
            return entries.ToArray();
        }

        private static bool ValidateCachedTracerHomeIntegrity(string cachedTracerHome, CacheIntegrityManifest integrityManifest)
        {
            CacheIntegrityEntry[] actualEntries;
            try
            {
                actualEntries = CreateCacheIntegrityEntries(cachedTracerHome, ignoreRootCacheMetadata: true);
            }
            catch
            {
                return false;
            }

            if (actualEntries.Length != integrityManifest.Entries.Length)
            {
                return false;
            }

            var relativePathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var actualByPath = new Dictionary<string, CacheIntegrityEntry>(relativePathComparer);
            foreach (var actualEntry in actualEntries)
            {
                if (actualByPath.ContainsKey(actualEntry.RelativePath))
                {
                    return false;
                }

                actualByPath.Add(actualEntry.RelativePath, actualEntry);
            }

            foreach (var expectedEntry in integrityManifest.Entries)
            {
                if (!actualByPath.TryGetValue(expectedEntry.RelativePath, out var actualEntry))
                {
                    return false;
                }

                if (actualEntry.IsDirectory != expectedEntry.IsDirectory ||
                    actualEntry.Length != expectedEntry.Length ||
                    !string.Equals(actualEntry.Sha256, expectedEntry.Sha256, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FileContentEquals(string path, string expectedContent)
        {
            return IsRegularFile(path) && File.ReadAllText(path) == expectedContent;
        }

        private static bool IsRegularFile(string path)
        {
            try
            {
                var attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Directory) == 0 &&
                       (attributes & FileAttributes.ReparsePoint) == 0;
            }
            catch
            {
                return false;
            }
        }

        private static string ComputeSha256(string path)
        {
            using var sha256 = SHA256.Create();
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = sha256.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string GetPathFromRelativePath(string rootPath, string relativePath)
        {
            return Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static IEnumerable<TracerHomeEntry> EnumerateTracerHomeEntries(string rootPath, bool ignoreRootCacheMetadata)
        {
            rootPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(rootPath);

            while (pendingDirectories.Count != 0)
            {
                var directoryPath = pendingDirectories.Pop();
                var entryPaths = new List<string>(Directory.EnumerateFileSystemEntries(directoryPath));
                entryPaths.Sort(StringComparer.Ordinal);
                foreach (var entryPath in entryPaths)
                {
                    var attributes = GetTracerHomeEntryAttributes(entryPath);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        // Symlinks would let a source or cache entry escape the validated tree between enumeration
                        // and copy/hash. Reject them instead of trying to canonicalize every possible target.
                        throw new IOException($"Tracer home entry '{entryPath}' must not be a symbolic link or reparse point.");
                    }

                    var isDirectory = (attributes & FileAttributes.Directory) != 0;
                    var relativePath = GetRelativePath(rootPath, entryPath);
                    if (IsCacheMetadataRelativePath(relativePath))
                    {
                        if (ignoreRootCacheMetadata && !isDirectory)
                        {
                            continue;
                        }

                        throw new IOException($"Tracer home entry '{entryPath}' conflicts with runner cache metadata.");
                    }

                    yield return new TracerHomeEntry(entryPath, relativePath, isDirectory);
                    if (isDirectory)
                    {
                        pendingDirectories.Push(entryPath);
                    }
                }
            }
        }

        private static FileAttributes GetTracerHomeEntryAttributes(string entryPath)
        {
            try
            {
                return File.GetAttributes(entryPath);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or SystemException)
            {
                throw new IOException($"Unable to inspect tracer home entry '{entryPath}'.", ex);
            }
        }

        private static bool IsCacheMetadataRelativePath(string relativePath)
        {
            return string.Equals(relativePath, CacheIntegrityFileName, StringComparison.Ordinal) ||
                   string.Equals(relativePath, CacheMarkerFileName, StringComparison.Ordinal);
        }

        private static string GetRelativePath(string rootPath, string path)
        {
            rootPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(rootPath));
            path = Path.GetFullPath(path);
            var pathComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!path.StartsWith(rootPath, pathComparison))
            {
                throw new IOException($"Path '{path}' is not under root '{rootPath}'.");
            }

            var relativePath = path.Substring(rootPath.Length)
                                   .Replace(Path.DirectorySeparatorChar, '/');
            if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
            {
                relativePath = relativePath.Replace(Path.AltDirectorySeparatorChar, '/');
            }

            return relativePath;
        }

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ||
                   path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                       ? path
                       : path + Path.DirectorySeparatorChar;
        }

        private static void CreatePrivateDirectory(string path)
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path))
            {
                ValidateExistingPrivateDirectory(path);
                return;
            }

            // Validate the nearest existing parent before creating the next segment. On POSIX this prevents
            // creating our private cache below a group/world-writable directory such as /tmp.
            var parentPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentPath) && !Directory.Exists(parentPath))
            {
                CreatePrivateDirectory(parentPath);
            }
            else if (!string.IsNullOrEmpty(parentPath))
            {
                ValidateExistingCacheParentDirectory(parentPath);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (Directory.Exists(path))
                {
                    throw new IOException($"Temporary tracer home directory '{path}' already exists.");
                }

                WindowsDirectoryAccess.CreatePrivateDirectory(path);
                ValidateExistingPrivateDirectory(path);
                return;
            }

            // Directory.CreateDirectory does not let us request 0700 on all supported TFMs, so call mkdir(2)
            // directly and then validate the resulting owner/mode before trusting the path.
            PosixDirectoryAccess.CreatePrivateDirectory(path);
            ValidateExistingPrivateDirectory(path);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    ValidateExistingPrivateDirectory(path);
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup only. The original tracer home remains in use when this fails.
            }
        }

        private static void ValidateExistingPrivateDirectory(string path)
        {
            ValidateExistingDirectory(path, requireCurrentUserOwner: true, allowGroupOrOtherWrite: false);
        }

        private static void ValidateExistingCacheParentDirectory(string path)
        {
            ValidateExistingDirectory(path, requireCurrentUserOwner: !RuntimeInformation.IsOSPlatform(OSPlatform.Windows), allowGroupOrOtherWrite: false);
        }

        private static void ValidateExistingDirectory(string path, bool requireCurrentUserOwner, bool allowGroupOrOtherWrite)
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                // A symlinked cache root can redirect the copy into an attacker-controlled tree even when the link
                // itself sits below a trusted parent, so reject reparse points before permission checks.
                throw new IOException($"Directory '{path}' must not be a symbolic link or reparse point.");
            }

            if ((attributes & FileAttributes.Directory) == 0)
            {
                throw new IOException($"Path '{path}' must be a directory.");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                WindowsDirectoryAccess.ValidateDirectoryAccess(path, requireCurrentUserOwner, allowGroupOrOtherWrite);
                return;
            }

            PosixDirectoryAccess.ValidateDirectoryAccess(path, requireCurrentUserOwner, allowGroupOrOtherWrite);
        }

        private readonly record struct CacheIntegrityEntry(string RelativePath, bool IsDirectory, long Length, string Sha256);

        private readonly record struct TracerHomeEntry(string FullPath, string RelativePath, bool IsDirectory);

        private sealed record CacheIntegrityManifest(CacheIntegrityEntry[] Entries, string Content);

        public record CIVisibilityOptions(bool EnableGacInstallation, bool EnableVsTestConsoleConfigModification, bool ReducePathLength)
        {
            public static CIVisibilityOptions None { get; } = new(false, false, false);
        }
    }
}
