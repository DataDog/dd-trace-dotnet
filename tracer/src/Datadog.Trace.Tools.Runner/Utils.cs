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
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Ci.Agent;
using Datadog.Trace.Ci.Sampling;
using Datadog.Trace.Configuration;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class Utils
    {
        public const string Profilerid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(string runnerFolder, Platform platform, CommonTracerSettings options)
        {
            // In the current nuspec structure RunnerFolder has the following format:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp3.1\any
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp2.1\any
            // And the Home folder is:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\home
            // So we have to go up 3 folders.
            string tracerHome = null;
            if (!string.IsNullOrEmpty(options.TracerHome))
            {
                tracerHome = options.TracerHome;
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

            if (platform == Platform.Windows)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64 || RuntimeInformation.OSArchitecture == Architecture.X86)
                {
                    tracerProfiler32 = FileExists(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
                }
                else
                {
                    WriteError($"Error: Windows {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
            }
            else if (platform == Platform.Linux)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                {
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, IsAlpine() ? "linux-musl-x64" : "linux-x64", "Datadog.Trace.ClrProfiler.Native.so"));
                }
                else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "linux-arm64", "Datadog.Trace.ClrProfiler.Native.so"));
                }
                else
                {
                    WriteError($"Error: Linux {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
            }
            else if (platform == Platform.MacOS)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                {
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "osx-x64", "Datadog.Trace.ClrProfiler.Native.dylib"));
                }
                else
                {
                    WriteError($"Error: macOS {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
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

            return envVars;
        }

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(string runnerFolder, Platform platform, LegacySettings options)
        {
            // In the current nuspec structure RunnerFolder has the following format:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp3.1\any
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\tools\netcoreapp2.1\any
            // And the Home folder is:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\[version]\datadog.trace.tools.runner\[version]\home
            // So we have to go up 3 folders.
            string tracerHome = null;
            if (!string.IsNullOrEmpty(options.TracerHomeFolder))
            {
                tracerHome = options.TracerHomeFolder;
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

            if (platform == Platform.Windows)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64 || RuntimeInformation.OSArchitecture == Architecture.X86)
                {
                    tracerProfiler32 = FileExists(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
                }
                else
                {
                    WriteError($"Error: Windows {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
            }
            else if (platform == Platform.Linux)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                {
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, IsAlpine() ? "linux-musl-x64" : "linux-x64", "Datadog.Trace.ClrProfiler.Native.so"));
                }
                else if (RuntimeInformation.OSArchitecture == Architecture.Arm64)
                {
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "linux-arm64", "Datadog.Trace.ClrProfiler.Native.so"));
                }
                else
                {
                    WriteError($"Error: Linux {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
            }
            else if (platform == Platform.MacOS)
            {
                if (RuntimeInformation.OSArchitecture == Architecture.X64)
                {
                    tracerProfiler64 = FileExists(Path.Combine(tracerHome, "osx-x64", "Datadog.Trace.ClrProfiler.Native.dylib"));
                }
                else
                {
                    WriteError($"Error: macOS {RuntimeInformation.OSArchitecture} architecture is not supported.");
                    return null;
                }
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

        public static async Task<bool> CheckAgentConnectionAsync(string agentUrl)
        {
            var env = new NameValueCollection();
            if (!string.IsNullOrWhiteSpace(agentUrl))
            {
                env["DD_TRACE_AGENT_URL"] = agentUrl;
            }

            var globalSettings = GlobalSettings.CreateDefaultConfigurationSource();
            globalSettings.Add(new NameValueConfigurationSource(env));
            var tracerSettings = new TracerSettings(globalSettings);
            var agentWriter = new CIAgentWriter(tracerSettings.Build(), new CISampler());

            try
            {
                if (!await agentWriter.Ping().ConfigureAwait(false))
                {
                    WriteError($"Error connecting to the Datadog Agent at {tracerSettings.Exporter.AgentUri}.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                WriteError($"Error connecting to the Datadog Agent at {tracerSettings.Exporter.AgentUri}.");
                AnsiConsole.WriteException(ex);
                return false;
            }
            finally
            {
                await agentWriter.FlushAndCloseAsync().ConfigureAwait(false);
            }

            return true;
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

        private static bool IsAlpine()
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
    }
}
