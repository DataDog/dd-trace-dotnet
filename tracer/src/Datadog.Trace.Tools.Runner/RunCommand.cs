// <copyright file="RunCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCommand : CommandWithExamples
    {
        private readonly ApplicationContext _applicationContext;
        private readonly RunSettings _runSettings;

        public RunCommand(ApplicationContext applicationContext)
            : base("run", "Run a command with the Datadog tracer enabled")
        {
            _applicationContext = applicationContext;
            _runSettings = new RunSettings(this);

            AddExample("dd-trace run -- dotnet myApp.dll");
            AddExample("dd-trace run -- MyApp.exe");

            this.SetHandler(Execute);
        }

        private void Execute(InvocationContext context)
        {
            var args = _runSettings.Command.GetValue(context);
            var program = args[0];
            var arguments = args.Length > 1 ? Utils.GetArgumentsAsString(args.Skip(1)) : string.Empty;

            // Get profiler environment variables
            if (!RunHelper.TryGetEnvironmentVariables(_applicationContext, context, _runSettings, out var profilerEnvironmentVariables))
            {
                context.ExitCode = 1;
                return;
            }

            AnsiConsole.WriteLine("Running: {0} {1}", program, arguments);

            if (Program.CallbackForTests != null)
            {
                Program.CallbackForTests(program, arguments, profilerEnvironmentVariables);
                return;
            }

            var processInfo = Utils.GetProcessStartInfo(program, Environment.CurrentDirectory, profilerEnvironmentVariables);
            if (!string.IsNullOrEmpty(arguments))
            {
                processInfo.Arguments = arguments;
            }

            context.ExitCode = Utils.RunProcess(processInfo, _applicationContext.TokenSource.Token);
        }
    }
}
