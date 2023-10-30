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
            var tracerHome = Utils.GetHomePath(_applicationContext.RunnerFolder);

            // pick the right one depending on the platform
            var ddDotnet = (platform: _applicationContext.Platform, arch: RuntimeInformation.OSArchitecture, musl: Utils.IsAlpine()) switch
            {
                (Platform.Windows, Architecture.X64, _) => Path.Combine(tracerHome, "win-x64", "dd-dotnet.exe"),
                (Platform.Windows, Architecture.X86, _) => Path.Combine(tracerHome, "win-x64", "dd-dotnet.exe"),
                (Platform.Linux, Architecture.X64, false) => Path.Combine(tracerHome, "linux-x64", "dd-dotnet"),
                (Platform.Linux, Architecture.X64, true) => Path.Combine(tracerHome, "linux-musl-x64", "dd-dotnet"),
                (Platform.Linux, Architecture.Arm64, false) => Path.Combine(tracerHome, "linux-arm64", "dd-dotnet"),
                (Platform.Linux, Architecture.Arm64, true) => Path.Combine(tracerHome, "linux-musl-arm64", "dd-dotnet"),
                var other => throw new NotSupportedException(
                    $"Unsupported platform/architecture combination: ({other.platform}{(other.musl ? " musl" : string.Empty)}/{other.arch})")
            };

            var commandLine = string.Join(' ', Environment.GetCommandLineArgs().Skip(1));

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

            var startInfo = new ProcessStartInfo(ddDotnet, commandLine) { UseShellExecute = false };
            startInfo.EnvironmentVariables["DD_INTERNAL_OVERRIDE_COMMAND"] = "dd-trace";

            var process = Process.Start(startInfo);
            process.WaitForExit();

            context.ExitCode = process.ExitCode;
        }
    }
}
