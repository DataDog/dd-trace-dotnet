// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.Tools.Runner.Crank;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Datadog.Trace.Tools.Runner
{
    internal class Program
    {
        internal static Action<string, string, Dictionary<string, string>> CallbackForTests { get; set; }

        internal static int Main(string[] args)
        {
            // Initializing
            var runnerFolder = AppContext.BaseDirectory;

            if (string.IsNullOrEmpty(runnerFolder))
            {
                runnerFolder = Path.GetDirectoryName(Environment.GetCommandLineArgs().FirstOrDefault());
            }

            Platform platform;

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                platform = Platform.Windows;
            }
            else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
            {
                platform = Platform.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                platform = Platform.MacOS;
            }
            else
            {
                Utils.WriteError("The current platform is not supported. Supported platforms are: Windows, Linux and MacOS.");
                return -1;
            }

            var applicationContext = new ApplicationContext(runnerFolder, platform);

            Console.CancelKeyPress += (_, e) => Console_CancelKeyPress(e, applicationContext);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CurrentDomain_ProcessExit(applicationContext);
            AppDomain.CurrentDomain.DomainUnload += (_, _) => CurrentDomain_ProcessExit(applicationContext);

            if (applicationContext.Platform == Platform.Linux)
            {
                // Make dd-dotnet executable
                var ddDotnet = Utils.GetDdDotnetPath(applicationContext);

                if (ddDotnet != null && File.Exists(ddDotnet))
                {
                    // Make sure the dd-dotnet binary is executable
                    System.Diagnostics.Process.Start("chmod", $"+x {ddDotnet}")!.WaitForExit();
                }
            }

            IEnumerable<HelpSectionDelegate> GetLayout(HelpContext context)
            {
                yield return HelpBuilder.Default.SynopsisSection();
                yield return HelpBuilder.Default.CommandUsageSection();

                if (context.Command is CommandWithExamples command && command.Examples.Count > 0)
                {
                    yield return CommandWithExamples.ExamplesSection();
                }

                yield return HelpBuilder.Default.CommandArgumentsSection();
                yield return HelpBuilder.Default.OptionsSection();
                yield return HelpBuilder.Default.SubcommandsSection();
                yield return HelpBuilder.Default.AdditionalArgumentsSection();
            }

            var localizationResources = new CustomLocalizationResources();

            var rootCommand = new CommandWithExamples("dd-trace");

            var builder = new CommandLineBuilder(rootCommand)
                .UseLocalizationResources(localizationResources)
                .UseHelp()
                .UseParseErrorReporting();

            builder.UseHelpBuilder(
                _ =>
                {
                    var helpBuilder = new HelpBuilder(localizationResources);
                    helpBuilder.CustomizeLayout(GetLayout);
                    return helpBuilder;
                });

            rootCommand.AddExample("dd-trace run --dd-env prod -- myApp --argument-for-my-app");
            rootCommand.AddExample("dd-trace ci configure azp");
            rootCommand.AddExample("dd-trace ci run -- dotnet test");

            var ciCommand = new Command("ci", "CI related commands");
            builder.Command.AddCommand(ciCommand);

            ciCommand.AddCommand(new ConfigureCiCommand(applicationContext));
            ciCommand.AddCommand(new RunCiCommand(applicationContext));
            ciCommand.AddCommand(new CrankCommand());

            builder.Command.AddCommand(new CheckCommand(applicationContext));

            builder.Command.AddCommand(new RunCommand(applicationContext));
            builder.Command.AddCommand(new AotCommand { IsHidden = true });
            builder.Command.AddCommand(new AnalyzeInstrumentationErrorsCommand { IsHidden = true });
            builder.Command.AddCommand(new CoverageMergerCommand { IsHidden = true });

            if (applicationContext.Platform == Platform.Windows)
            {
                var gacCommand = new Command("gac", "Install or Uninstall a .NET Framework assembly to the GAC");
                builder.Command.AddCommand(gacCommand);

#pragma warning disable CA1416
                gacCommand.AddCommand(new GacGetCommand());
                gacCommand.AddCommand(new GacInstallCommand());
                gacCommand.AddCommand(new GacUninstallCommand());
#pragma warning restore CA1416
            }

            var parser = builder.Build();

            var parseResult = parser.Parse(args);

            if (parseResult.Errors.Count > 0)
            {
                if (parseResult.Tokens.Count > 0 && parseResult.CommandResult.Command == builder.Command)
                {
                    var legacyParser = new CommandLineBuilder(new LegacyCommand(applicationContext))
                        .Build();

                    var legacyParseResult = legacyParser.Parse(args);

                    if (legacyParseResult.Errors.Count == 0)
                    {
                        return legacyParseResult.Invoke();
                    }
                }
            }

            try
            {
                return parseResult.Invoke();
            }
            catch (Exception ex)
            {
                AnsiConsole.Write(new Markup($"[red]Error:[/] {ex.Message.EscapeMarkup()}{Environment.NewLine}"));
                return 1;
            }
        }

        private static void Console_CancelKeyPress(ConsoleCancelEventArgs e, ApplicationContext context)
        {
            e.Cancel = true;
            context.TokenSource.Cancel();
        }

        private static void CurrentDomain_ProcessExit(ApplicationContext context)
        {
            context.TokenSource.Cancel();
        }
    }
}
