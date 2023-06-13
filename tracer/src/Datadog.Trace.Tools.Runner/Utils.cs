// <copyright file="Utils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Sampling;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.Util;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class Utils
    {
        public const string Profilerid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(string runnerFolder, Platform platform, CommonTracerSettings options)
        {
            var envVars = GetBaseProfilerEnvironmentVariables(runnerFolder, platform, options.TracerHome);

            if (!string.IsNullOrWhiteSpace(options.Environment))
            {
                envVars[ConfigurationKeys.Environment] = options.Environment;
            }

            if (!string.IsNullOrWhiteSpace(options.Service))
            {
                envVars[ConfigurationKeys.ServiceName] = options.Service;
            }

            if (!string.IsNullOrWhiteSpace(options.Version))
            {
                envVars[ConfigurationKeys.ServiceVersion] = options.Version;
            }

            if (!string.IsNullOrWhiteSpace(options.AgentUrl))
            {
                envVars[ConfigurationKeys.AgentUri] = options.AgentUrl;
            }

            return envVars;
        }

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(string runnerFolder, Platform platform, LegacySettings options)
        {
            var envVars = GetBaseProfilerEnvironmentVariables(runnerFolder, platform, options.TracerHomeFolder);

            if (!string.IsNullOrWhiteSpace(options.Environment))
            {
                envVars["DD_ENV"] = options.Environment;
            }

            if (!string.IsNullOrWhiteSpace(options.Service))
            {
                envVars["DD_SERVICE"] = options.Service;
            }

            if (!string.IsNullOrWhiteSpace(options.Version))
            {
                envVars["DD_VERSION"] = options.Version;
            }

            if (!string.IsNullOrWhiteSpace(options.AgentUrl))
            {
                envVars["DD_TRACE_AGENT_URL"] = options.AgentUrl;
            }

            if (!string.IsNullOrWhiteSpace(options.EnvironmentValues))
            {
                foreach (var keyValue in options.EnvironmentValues.Split(',', StringSplitOptions.RemoveEmptyEntries))
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

        public static void SetCommonTracerSettingsToCurrentProcess(CommonTracerSettings options)
        {
            // Settings back DD_ENV to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(options.Environment))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.Environment, options.Environment);
            }

            // Settings back DD_SERVICE to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(options.Service))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ServiceName, options.Service);
            }

            // Settings back DD_VERSION to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(options.Version))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.ServiceVersion, options.Version);
            }

            // Settings back DD_TRACE_AGENT_URL to use it in the current process (eg for CIVisibility's TestSession)
            if (!string.IsNullOrWhiteSpace(options.AgentUrl))
            {
                EnvironmentHelpers.SetEnvironmentVariable(ConfigurationKeys.AgentUri, options.AgentUrl);
            }
        }

        public static string DirectoryExists(string name, params string[] paths)
        {
            string folderName = null;

            try
            {
                for (int i = 0; i < paths.Length; i++)
                {
                    if (Directory.Exists(paths[i]))
                    {
                        folderName = paths[i];
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
                using (Process childProcess = new Process())
                {
                    childProcess.StartInfo = startInfo;
                    childProcess.EnableRaisingEvents = true;
                    childProcess.Start();

                    using (cancellationToken.Register(() =>
                    {
                        try
                        {
                            childProcess.Kill();
                        }
                        catch
                        {
                            // .
                        }
                    }))
                    {
                        childProcess.WaitForExit();
                        return cancellationToken.IsCancellationRequested ? 1 : childProcess.ExitCode;
                    }
                }
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
            configurationSource.AddInternal(GlobalConfigurationSource.Instance);
            configurationSource.AddInternal(new NameValueConfigurationSource(env, ConfigurationOrigins.EnvVars));

            var tracerSettings = new TracerSettings(configurationSource, new ConfigurationTelemetry());
            var settings = new ImmutableTracerSettings(tracerSettings);

            var discoveryService = DiscoveryService.Create(
                settings.Exporter,
                tcpTimeout: TimeSpan.FromSeconds(5),
                initialRetryDelayMs: 10,
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
            AnsiConsole.MarkupLine($"[red]{message.EscapeMarkup()}[/]");
        }

        internal static void WriteWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow]{message.EscapeMarkup()}[/]");
        }

        internal static void WriteSuccess(string message)
        {
            AnsiConsole.MarkupLine($"[green]{message.EscapeMarkup()}[/]");
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

        private static Dictionary<string, string> GetBaseProfilerEnvironmentVariables(string runnerFolder, Platform platform, string tracerHomeFolder)
        {
            // In the current nuspec structure RunnerFolder has the following format:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp3.1\any
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp2.1\any
            // And the Home folder is:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\home
            // So we have to go up 3 folders.
            string tracerHome = null;
            if (!string.IsNullOrEmpty(tracerHomeFolder))
            {
                tracerHome = tracerHomeFolder;
                if (!Directory.Exists(tracerHome))
                {
                    WriteError("Error: The specified home folder doesn't exist.");
                }
            }

            tracerHome ??= DirectoryExists("Home", Path.Combine(runnerFolder, "..", "..", "..", "home"), Path.Combine(runnerFolder, "home"));

            if (tracerHome == null)
            {
                WriteError("Error: The home directory can't be found. Check that the tool is correctly installed, or use --tracer-home to set a custom path.");
                return null;
            }

            string tracerMsBuild = FileExists(Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.MSBuild.dll"));
            string tracerProfiler32 = string.Empty;
            string tracerProfiler64 = string.Empty;
            string tracerProfilerArm64 = null;
            string ldPreload = string.Empty;

            if (platform == Platform.Windows)
            {
                tracerProfiler32 = FileExists(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
                tracerProfiler64 = FileExists(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
                tracerProfilerArm64 = FileExistsOrNull(Path.Combine(tracerHome, "win-ARM64EC", "Datadog.Trace.ClrProfiler.Native.dll"));
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
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "linux-arm64", "Datadog.Trace.ClrProfiler.Native.so"));
                    tracerProfilerArm64 = tracerProfiler64;
                    ldPreload = FileExists(Path.Combine(tracerHome, "linux-arm64", "Datadog.Linux.ApiWrapper.x64.so"));
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

            return envVars;
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
    }
}
