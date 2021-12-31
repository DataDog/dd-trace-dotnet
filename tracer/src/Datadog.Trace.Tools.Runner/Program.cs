// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spectre.Console.Cli;

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
                Console.Error.WriteLine("The current platform is not supported. Supported platforms are: Windows, Linux and MacOS.");
                return -1;
            }

            var applicationContext = new ApplicationContext(runnerFolder, platform);

            Console.CancelKeyPress += (_, e) => Console_CancelKeyPress(e, applicationContext);
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CurrentDomain_ProcessExit(applicationContext);
            AppDomain.CurrentDomain.DomainUnload += (_, _) => CurrentDomain_ProcessExit(applicationContext);

            var app = new CommandApp<LegacyCommand>();

            app.Configure(c =>
            {
                c.Settings.Registrar.RegisterInstance(applicationContext);

                c.AddExample(new[] { "--set-ci" });
                c.AddExample(new[] { "dotnet", "test" });
                c.AddExample(new[] { "dd-env=ci", "dotnet", "test" });
                c.AddExample(new[] { "--agent-url=http://agent:8126", "dotnet", "test" });
            });

            return app.Run(args);
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
