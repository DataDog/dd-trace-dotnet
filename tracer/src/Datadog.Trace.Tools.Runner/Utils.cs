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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class Utils
    {
        public const string Profilerid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

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

            // Settings back DD_ENV to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(environment))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Environment, environment);
            }

            var service = options.Service.GetValue(context);

            // Settings back DD_SERVICE to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(service))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ServiceName, service);
            }

            var version = options.Version.GetValue(context);

            // Settings back DD_VERSION to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(version))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ServiceVersion, version);
            }

            var agentUrl = options.AgentUrl.GetValue(context);

            // Settings back DD_TRACE_AGENT_URL to use it in the current process (eg for CIVisibility's TestSession)
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

        public static async Task<(AgentConfiguration Configuration, DiscoveryService DiscoveryService)> CheckAgentConnectionAsync(string agentUrl)
        {
            var env = new NameValueCollection();
            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                env[ConfigurationKeys.AgentUri] = agentUrl;
            }

            var configurationSource = new CompositeConfigurationSourceInternal();
            configurationSource.AddInternal(new NameValueConfigurationSource(env, ConfigurationOrigins.EnvVars));
            configurationSource.AddInternal(GlobalConfigurationSource.Instance);

            var tracerSettings = new TracerSettings(configurationSource, new ConfigurationTelemetry(), new OverrideErrorLog());
            var settings = new ImmutableTracerSettings(tracerSettings, unusedParamNotToUsePublicApi: true);

            Log.Debug("Creating DiscoveryService for: {AgentUriInternal}", settings.ExporterInternal.AgentUriInternal);
            var discoveryService = DiscoveryService.Create(
                settings.ExporterInternal,
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
                           WriteError($"Error connecting to the Datadog Agent at {tracerSettings.ExporterInternal.AgentUriInternal}.");
                           tcs.TrySetResult(null);
                       }))
            {
                var configuration = await tcs.Task.ConfigureAwait(false);
                await discoveryService.DisposeAsync().ConfigureAwait(false);
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
                // We try to use reduce the length of the path using a temporary folder.
                var tempFolder = Path.Combine(Path.GetTempPath(), "dd");
                if (tempFolder.Length < tracerHome.Length)
                {
                    try
                    {
                        CopyFilesRecursively(tracerHome, tempFolder);
                        tracerHome = tempFolder;
                    }
                    catch
                    {
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
                    using var container = Gac.NativeMethods.CreateAssemblyCache();
                    var asmInfo = new Gac.AssemblyInfo();
                    var hr = container.AssemblyCache.QueryAssemblyInfo(Gac.QueryAssemblyInfoFlag.QUERYASMINFO_FLAG_GETSIZE, "Datadog.Trace", ref asmInfo);
                    if (hr == 0 && asmInfo.AssemblyFlags == Gac.AssemblyInfoFlags.ASSEMBLYINFO_FLAG_INSTALLED)
                    {
                        // Datadog.Trace is in the GAC, do nothing
                        Log.Information("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace is already installed in the gac.");
                        return true;
                    }

                    Log.Warning("EnsureDatadogTraceIsInTheGac [Built-in]: Datadog.Trace is not in the GAC, let's try to install it.");

                    if (Gac.AdministratorHelper.IsElevated)
                    {
                        WriteInfo("Datadog.Trace is not installed in the GAC, installing it...");

                        hr = container.AssemblyCache.InstallAssembly(0, datadogTraceDllPath, IntPtr.Zero);
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
            var stringBuilder = StringBuilderCache.Acquire(100);

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
            // Now Create all of the directories
            foreach (var dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            // Copy all the files & Replaces any files with the same name
            foreach (var newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        public record CIVisibilityOptions(bool EnableGacInstallation, bool EnableVsTestConsoleConfigModification, bool ReducePathLength)
        {
            public static CIVisibilityOptions None { get; } = new(false, false, false);
        }
    }
}
