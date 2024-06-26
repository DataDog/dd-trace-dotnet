// <copyright file="CheckCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class CheckCommand : Command
    {
        private readonly ApplicationContext _applicationContext;

        public CheckCommand(ApplicationContext applicationContext)
            : base("check")
        {
            _applicationContext = applicationContext;

            if (applicationContext.Platform == Platform.MacOS)
            {
                this.SetHandler(context =>
                {
                    Utils.WriteError("The check command is not supported on MacOS.");
                    context.ExitCode = 1;
                });
            }
            else
            {
                AddArgument(Command);
                this.SetHandler(Execute);
            }
        }

        public Argument<string[]> Command { get; } = new("command", CommandLineHelpers.ParseArrayArgument, isDefault: true);

        private void Execute(InvocationContext context)
        {
            // pick the right one depending on the platform
            var ddDotnet = Utils.GetDdDotnetPath(_applicationContext);

            if (!File.Exists(ddDotnet))
            {
                Utils.WriteError($"dd-dotnet not found at {ddDotnet}");
                context.ExitCode = 1;
                return;
            }

            if (_applicationContext.Platform == Platform.Linux)
            {
                // Make sure the dd-dotnet binary is executable
                Process.Start("chmod", $"+x {ddDotnet}")!.WaitForExit();
            }

            var startInfo = new ProcessStartInfo(ddDotnet) { UseShellExecute = false };

            foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
            {
                startInfo.ArgumentList.Add(arg);
            }

            startInfo.EnvironmentVariables["DD_INTERNAL_OVERRIDE_COMMAND"] = "dd-trace";

            var process = Process.Start(startInfo);
            process.WaitForExit();

            context.ExitCode = process.ExitCode;
        }
    }
}
