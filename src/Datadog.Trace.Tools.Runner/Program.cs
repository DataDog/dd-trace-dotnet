using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using CommandLine;
using CommandLine.Text;

namespace Datadog.Trace.Tools.Runner
{
    internal class Program
    {
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

            RunnerFolder = Path.GetDirectoryName(location);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Platform = Platform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Platform = Platform.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Platform = Platform.MacOS;
            }
            else
            {
                Console.Error.WriteLine("The current platform is not supported. Supported platforms are: Windows, Linux and MacOS.");
                Environment.Exit(-1);
                return;
            }

            // ***

            Console.CancelKeyPress += Console_CancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += CurrentDomain_ProcessExit;

            Parser parser = new Parser(settings =>
            {
                settings.AutoHelp = true;
                settings.AutoVersion = true;
                settings.EnableDashDash = true;
                settings.HelpWriter = null;
            });

            ParserResult<Options> result = parser.ParseArguments<Options>(args);
            result.MapResult(ParsedOptions, errors => ParsedErrors(result, errors));
        }

        private static int ParsedOptions(Options options)
        {
            string[] args = options.Value.ToArray();

            // Start logic

            Dictionary<string, string> profilerEnvironmentVariables = Utils.GetProfilerEnvironmentVariables(RunnerFolder, Platform, options);

            if (options.SetEnvironmentVariables)
            {
                Console.WriteLine("Setting up the environment variables.");
                CIConfiguration.SetupCIEnvironmentVariables(profilerEnvironmentVariables);
            }
            else
            {
                string cmdLine = string.Join(' ', args);
                if (!string.IsNullOrWhiteSpace(cmdLine))
                {
                    Console.WriteLine("Running: " + cmdLine);

                    ProcessStartInfo processInfo = Utils.GetProcessStartInfo(args[0], Environment.CurrentDirectory, profilerEnvironmentVariables);
                    if (args.Length > 1)
                    {
                        processInfo.Arguments = string.Join(' ', args.Skip(1).ToArray());
                    }

                    return Utils.RunProcess(processInfo, _tokenSource.Token);
                }
            }

            return 0;
        }

        private static int ParsedErrors(ParserResult<Options> result, IEnumerable<Error> errors)
        {
            HelpText helpText = null;
            if (errors.IsVersion())
            {
                helpText = HelpText.AutoBuild(result);
            }
            else
            {
                helpText = HelpText.AutoBuild(
                    result,
                    h =>
                    {
                        h.Heading = "Datadog APM Auto-instrumentation Runner";
                        h.AddNewLineBetweenHelpSections = true;
                        h.AdditionalNewLineAfterOption = false;
                        return h;
                    },
                    e =>
                    {
                        return e;
                    });
            }

            Console.WriteLine(helpText);
            return 1;
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
    }
}
