using System;
using System.Collections;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Datadog.Trace.Tools.Runner
{
    internal class Program
    {
        private const string PROFILERID = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        private static RootCommand _rootCommand;

        private static IConsole _console;

        private static object _globalLock = new object();

        private static System.Diagnostics.Process _childProcess = null;

        private static string RunnerFolder { get; set; }

        private static Platform Platform { get; set; }

        private static void Main(string[] args)
        {
            RunnerFolder = Path.GetDirectoryName(RootCommand.ExecutablePath);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Platform = Platform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Platform = Platform.Linux;
            }

            _rootCommand = new RootCommand($"Datadog .NET Autoinstrumentation Runner ({Platform}-{(Environment.Is64BitProcess ? "x64" : "x86")})");
            _rootCommand.Add(new Option<bool>(new string[] { "/init-ci", "-init-ci" }, "Setup profiler for all following CI steps."));
            _rootCommand.TreatUnmatchedTokensAsErrors = false;
            _rootCommand.Handler = CommandHandler.Create(new Func<bool, IConsole, CancellationToken, int>(Handler));
            _rootCommand.Invoke(args);
        }

        private static int Handler(bool initCI, IConsole console, CancellationToken cancellationToken)
        {
            try
            {
                _console = console;

                if (Platform == Platform.Unknown)
                {
                    console.Error.WriteLine("The current platform is not supported. Supported platforms are: Windows and Linux.");
                    return 1;
                }

                if (initCI)
                {
                    console.Out.WriteLine("Setting up the CI environment variables.");
                }
                else
                {
                    string cmdLine = Environment.CommandLine.Replace(RootCommand.ExecutablePath, string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(cmdLine))
                    {
                        console.Out.WriteLine("Running: " + cmdLine);

                        string[] args = Environment.GetCommandLineArgs();
                        ProcessStartInfo processInfo = GetProcessStartInfo(args[1], Environment.CurrentDirectory, GetProfilerEnvironmentVariables());
                        if (args.Length > 2)
                        {
                            processInfo.Arguments = string.Join(' ', args.Skip(2).ToArray());
                        }

                        return RunProcess(processInfo, cancellationToken);
                    }

                    console.Out.WriteLine(RunnerFolder);
                }

                return 0;
            }
            catch (OperationCanceledException)
            {
                return 1;
            }
        }

        private static Dictionary<string, string> GetProfilerEnvironmentVariables()
        {
            string tracerHome = RunnerFolder;
            string tracerMsBuild = Path.Combine(tracerHome, "Datadog.Trace.MSBuild.dll");
            string tracerIntegrations = Path.Combine(tracerHome, "integrations.json");
            string tracerProfiler32 = string.Empty;
            string tracerProfiler64 = string.Empty;

            if (Platform == Platform.Windows)
            {
                tracerProfiler32 = Path.Combine(tracerHome, "runtimes", "win-x86", "native", "Datadog.Trace.ClrProfiler.Native.dll");
                tracerProfiler64 = Path.Combine(tracerHome, "runtimes", "win-x64", "native", "Datadog.Trace.ClrProfiler.Native.dll");
            }
            else if (Platform == Platform.Linux)
            {
                tracerProfiler64 = Path.Combine(tracerHome, "runtimes", "linux-x64", "native", "Datadog.Trace.ClrProfiler.Native.so");
            }

            return new Dictionary<string, string>
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
                _childProcess = new System.Diagnostics.Process();
                _childProcess.StartInfo = startInfo;
                _childProcess.OutputDataReceived += Process_OutputDataReceived;
                _childProcess.ErrorDataReceived += Process_ErrorDataReceived;
                _childProcess.EnableRaisingEvents = true;
                _childProcess.Start();
                _childProcess.BeginOutputReadLine();
                _childProcess.BeginErrorReadLine();

                while (!(_childProcess.WaitForExit(250) || cancellationToken.IsCancellationRequested))
                {
                }

                int exitCode = 0;

                if (cancellationToken.IsCancellationRequested)
                {
                    _childProcess.StandardInput.Close();
                    _childProcess.Kill();
                    return -1;
                }
                else
                {
                    exitCode = _childProcess.ExitCode;
                }

                lock (_globalLock)
                {
                    _childProcess.Dispose();
                    _childProcess = null;
                }

                return exitCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return -1;
        }

        private static void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            lock (_globalLock)
            {
                _console.Error.Write(e.Data);
            }
        }

        private static void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            lock (_globalLock)
            {
                _console.Out.WriteLine(e.Data);
            }
        }
    }
}
