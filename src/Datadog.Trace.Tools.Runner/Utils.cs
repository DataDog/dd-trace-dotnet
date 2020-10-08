using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Datadog.Trace.Tools.Runner
{
    internal class Utils
    {
        public const string PROFILERID = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        public static Dictionary<string, string> GetProfilerEnvironmentVariables(string runnerFolder, Platform platform, Options options)
        {
            // In the current nuspec structure RunnerFolder has the following format:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\1.19.3\datadog.trace.tools.runner\1.19.3\tools\netcoreapp3.1\any
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\1.19.3\datadog.trace.tools.runner\1.19.3\tools\netcoreapp2.1\any
            // And the Home folder is:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\1.19.3\datadog.trace.tools.runner\1.19.3\home
            // So we have to go up 3 folders.

            string tracerHome = EnsureFolder(options.TracerHomeFolder ?? Path.Combine(runnerFolder, "..", "..", "..", "home"));
            string tracerMsBuild = EnsureFile(Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.MSBuild.dll"));
            string tracerIntegrations = EnsureFile(Path.Combine(tracerHome, "integrations.json"));
            string tracerProfiler32 = string.Empty;
            string tracerProfiler64 = string.Empty;

            if (platform == Platform.Windows)
            {
                tracerProfiler32 = EnsureFile(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
                tracerProfiler64 = EnsureFile(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
            }
            else if (platform == Platform.Linux)
            {
                tracerProfiler64 = EnsureFile(Path.Combine(tracerHome, "linux-x64", "Datadog.Trace.ClrProfiler.Native.so"));
            }

            var envVars = new Dictionary<string, string>
            {
                ["DD_DOTNET_TRACER_HOME"] = tracerHome,
                ["DD_DOTNET_TRACER_MSBUILD"] = tracerMsBuild,
                ["DD_INTEGRATIONS"] = tracerIntegrations,
                ["CORECLR_ENABLE_PROFILING"] = "1",
                ["CORECLR_PROFILER"] = PROFILERID,
                ["CORECLR_PROFILER_PATH_32"] = tracerProfiler32,
                ["CORECLR_PROFILER_PATH_64"] = tracerProfiler64,
                ["COR_ENABLE_PROFILING"] = "1",
                ["COR_PROFILER"] = PROFILERID,
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

        public static string EnsureFolder(string folderName)
        {
            try
            {
                if (!Directory.Exists(folderName))
                {
                    Console.Error.WriteLine($"Error: The folder '{folderName}' can't be found.");
                }
            }
            catch { }

            return folderName;
        }

        public static string EnsureFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.Error.WriteLine($"Error: The file '{filePath}' can't be found.");
                }
            }
            catch { }

            return filePath;
        }

        public static ProcessStartInfo GetProcessStartInfo(string filename, string currentDirectory, IDictionary<string, string> environmentVariables)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo(filename)
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
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
                    childProcess.OutputDataReceived += (sender, e) => Console.WriteLine(e.Data);
                    childProcess.ErrorDataReceived += (sender, e) => Console.Error.WriteLine(e.Data);
                    childProcess.EnableRaisingEvents = true;
                    childProcess.Start();
                    childProcess.BeginOutputReadLine();
                    childProcess.BeginErrorReadLine();

                    using (cancellationToken.Register(() =>
                    {
                        childProcess?.StandardInput.Close();
                        childProcess?.Kill();
                    }))
                    {
                        childProcess.WaitForExit();
                        return cancellationToken.IsCancellationRequested ? -1 : childProcess.ExitCode;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return -1;
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
                Console.Error.WriteLine($"Error while reading environment variable {key}: {ex}");
            }

            return defaultValue;
        }
    }
}
