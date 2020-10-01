using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;

namespace Datadog.Trace.Tools.Runner
{
    internal class Program
    {
        private const string PROFILERID = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        private static Parser _parser;

        private static CancellationTokenSource _tokenSource = new CancellationTokenSource();

        private static string RunnerFolder { get; set; }

        private static Platform Platform { get; set; }

        private static void Main(string[] args)
        {
            // Initializing
            string executablePath = (Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly()).Location;
            string location = executablePath;
            if (string.IsNullOrEmpty(location))
            {
                location = Environment.GetCommandLineArgs().FirstOrDefault();
            }

            string executableName = Path.GetFileNameWithoutExtension(location).Replace(" ", string.Empty);

            RunnerFolder = Path.GetDirectoryName(location);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Platform = Platform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Platform = Platform.Linux;
            }
            else
            {
                Console.Error.WriteLine("The current platform is not supported. Supported platforms are: Windows and Linux.");
                Environment.Exit(-1);
                return;
            }

            // ***

            Console.CancelKeyPress += Console_CancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_ProcessExit;

            _parser = new Parser(settings =>
            {
                settings.AutoHelp = true;
                settings.AutoVersion = true;
                settings.EnableDashDash = true;
                settings.HelpWriter = Console.Out;
            });

            _parser.ParseArguments<Options>(args)
                .MapResult(ParsedOptions, ParsedErrors);
        }

        private static int ParsedOptions(Options options)
        {
            // Extract remaining args not parsed by options.
            string[] optionsArgs = SplitArgs(_parser.FormatCommandLine(options));
            string[] currentArgs = Environment.GetCommandLineArgs();
            List<string> remainingArgs = new List<string>(currentArgs.Length);
            for (int i = 1; i < currentArgs.Length; i++)
            {
                if (Array.IndexOf(optionsArgs, currentArgs[i]) == -1)
                {
                    remainingArgs.Add(currentArgs[i]);
                }
            }

            string[] args = remainingArgs.ToArray();

            // Start logic

            if (options.InitCI)
            {
                Console.WriteLine("Setting up the CI environment variables.");
            }
            else
            {
                string cmdLine = string.Join(' ', args);
                if (!string.IsNullOrWhiteSpace(cmdLine))
                {
                    Console.WriteLine("Running: " + cmdLine);

                    ProcessStartInfo processInfo = GetProcessStartInfo(args[0], Environment.CurrentDirectory, GetProfilerEnvironmentVariables(options));
                    if (args.Length > 1)
                    {
                        processInfo.Arguments = string.Join(' ', args.Skip(1).ToArray());
                    }

                    return RunProcess(processInfo, _tokenSource.Token);
                }
            }

            return 0;
        }

        private static int ParsedErrors(IEnumerable<Error> errors)
        {
            return 1;
        }

        private static Dictionary<string, string> GetProfilerEnvironmentVariables(Options options)
        {
            // In the current nuspec structure RunnerFolder has the following format:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\1.19.3\datadog.trace.tools.runner\1.19.3\tools\netcoreapp3.1\any
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\1.19.3\datadog.trace.tools.runner\1.19.3\tools\netcoreapp2.1\any
            // And the Home folder is:
            //  C:\Users\[user]\.dotnet\tools\.store\datadog.trace.tools.runner\1.19.3\datadog.trace.tools.runner\1.19.3\home
            // So we have to go up 3 folders.

            string tracerHome = EnsureFolder(Path.Combine(RunnerFolder, "..", "..", "..", "home"));
            string tracerMsBuild = EnsureFile(Path.Combine(tracerHome, "netstandard2.0", "Datadog.Trace.MSBuild.dll"));
            string tracerIntegrations = EnsureFile(Path.Combine(tracerHome, "integrations.json"));
            string tracerProfiler32 = string.Empty;
            string tracerProfiler64 = string.Empty;

            if (Platform == Platform.Windows)
            {
                tracerProfiler32 = EnsureFile(Path.Combine(tracerHome, "win-x86", "Datadog.Trace.ClrProfiler.Native.dll"));
                tracerProfiler64 = EnsureFile(Path.Combine(tracerHome, "win-x64", "Datadog.Trace.ClrProfiler.Native.dll"));
            }
            else if (Platform == Platform.Linux)
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

            if (!string.IsNullOrWhiteSpace(options.AgentUrl))
            {
                envVars["DD_TRACE_AGENT_URL"] = options.AgentUrl;
            }

            return envVars;
        }

        private static string EnsureFolder(string folderName)
        {
            if (!Directory.Exists(folderName))
            {
                Console.Error.WriteLine($"Error: The folder '{folderName}' can't be found.");
            }

            return folderName;
        }

        private static string EnsureFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"Error: The file '{filePath}' can't be found.");
            }

            return filePath;
        }

        private static ProcessStartInfo GetProcessStartInfo(string filename, string currentDirectory, IDictionary<string, string> environmentVariables)
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

        private static int RunProcess(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            try
            {
                using (Process childProcess = new Process())
                {
                    childProcess.StartInfo = startInfo;
                    childProcess.OutputDataReceived += Process_OutputDataReceived;
                    childProcess.ErrorDataReceived += Process_ErrorDataReceived;
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
                        while (!childProcess.WaitForExit(250))
                        {
                        }

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

        private static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.Error.Write(e.Data);
        }

        private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _tokenSource.Cancel();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            _tokenSource.Cancel();
        }

        private static string[] SplitArgs(string command, bool keepQuote = false)
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

        private class Options
        {
            [Option("init-ci", Required = false, Default = false, HelpText = "Setup the clr profiler for the following ci steps.")]
            public bool InitCI { get; set; }

            [Option("env", Required = false, HelpText = "Environment name.")]
            public string Environment { get; set; }

            [Option("agent-url", Required = false, HelpText = "Datadog trace agent url.")]
            public string AgentUrl { get; set; }
        }
    }
}
