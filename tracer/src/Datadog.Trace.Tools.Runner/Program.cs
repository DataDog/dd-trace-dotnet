// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Rendering;

namespace Datadog.Trace.Tools.Runner
{
    internal class Program
    {
        private static readonly string[] KnownCommands = new[] { "ci", "run", "check", "apply-aot" };

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

            try
            {
                var app = new CommandApp();

                app.Configure(config =>
                {
                    ConfigureApp(config, applicationContext);
                });

                return app.Run(args);
            }
            catch (CommandParseException ex) when (!IsKnownCommand(args))
            {
                try
                {
                    return ExecuteLegacyCommandLine(args, applicationContext);
                }
                catch (CommandRuntimeException)
                {
                    // Command line is invalid for both parsers
                    if (ex.Pretty != null)
                    {
                        AnsiConsole.Write(ex.Pretty);
                    }
                    else
                    {
                        AnsiConsole.WriteException(ex);
                    }

                    return 1;
                }
            }
            catch (Exception ex)
            {
                foreach (var render in GetRenderableErrorMessage(ex))
                {
                    AnsiConsole.Write(render);
                }

                return 1;
            }
        }

        private static void ConfigureApp(IConfigurator config, ApplicationContext applicationContext)
        {
            config.UseStrictParsing();
            config.Settings.Registrar.RegisterInstance(applicationContext);

            config.SetApplicationName("dd-trace");

            // Activate the exceptions, so we can fallback on the old syntax if the arguments can't be parsed
            config.PropagateExceptions();

            config.AddExample("run --dd-env prod -- myApp --argument-for-my-app".Split(' '));
            config.AddExample("ci configure azp".Split(' '));
            config.AddExample("ci run -- dotnet test".Split(' '));

            config.AddBranch(
                "ci",
                c =>
                {
                    c.SetDescription("CI related commands");

                    c.AddCommand<ConfigureCiCommand>("configure")
                        .WithDescription("Set the environment variables for the CI")
                        .WithExample("ci configure azp".Split(' '));
                    c.AddCommand<RunCiCommand>("run")
                        .WithDescription("Run a command and instrument the tests")
                        .WithExample("ci run -- dotnet test".Split(' '));
                });

            config.AddBranch(
                "check",
                c =>
                {
                    c.AddCommand<CheckProcessCommand>("process");
                    c.AddCommand<CheckAgentCommand>("agent");

                    if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                    {
                        c.AddCommand<CheckIisCommand>("iis");
                    }
                });

            config.AddCommand<RunCommand>("run")
                .WithDescription("Run a command with the Datadog tracer enabled")
                .WithExample("run -- dotnet myApp.dll".Split(' '));

            config.AddCommand<AotCommand>("apply-aot")
                  .WithDescription("Apply AOT automatic instrumentation on application folder")
                  .WithExample("apply-aot c:\\input\\ c:\\output\\".Split(' '))
                  .IsHidden();
        }

        private static int ExecuteLegacyCommandLine(string[] args, ApplicationContext applicationContext)
        {
            // Try executing the command with the legacy syntax
            var app = new CommandApp<LegacyCommand>();

            app.Configure(c =>
            {
                c.Settings.Registrar.RegisterInstance(applicationContext);
                c.PropagateExceptions();
            });

            return app.Run(args);
        }

        // Extracted from Spectre.Console source code
        // This is needed because we disable the default error handling to try the fallback legacy parser
        private static List<IRenderable> GetRenderableErrorMessage(Exception ex, bool convert = true)
        {
            if (ex is CommandAppException renderable && renderable.Pretty != null)
            {
                return new List<IRenderable> { renderable.Pretty };
            }

            if (convert)
            {
                var converted = new List<IRenderable>
                {
                    new Markup($"[red]Error:[/] {ex.Message.EscapeMarkup()}{Environment.NewLine}")
                };

                // Got a renderable inner exception?
                if (ex.InnerException != null)
                {
                    var innerRenderable = GetRenderableErrorMessage(ex.InnerException, convert: false);
                    if (innerRenderable != null)
                    {
                        converted.AddRange(innerRenderable);
                    }
                }

                return converted;
            }

            return null;
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

        private static bool IsKnownCommand(string[] args)
        {
            return args.Length > 0 && KnownCommands.Contains(args[0]);
        }
    }
}
