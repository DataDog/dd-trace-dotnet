// <copyright file="RunCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCommand : Command<RunSettings>
    {
        private readonly ApplicationContext _applicationContext;

        public RunCommand(ApplicationContext applicationContext)
        {
            _applicationContext = applicationContext;
        }

        public override int Execute(CommandContext context, RunSettings settings)
        {
            var args = RunHelper.GetArguments(context, settings);
            var program = args[0];
            var arguments = args.Count > 1 ? Utils.GetArgumentsAsString(args.Skip(1)) : string.Empty;

            // Get profiler environment variables
            if (!RunHelper.TryGetEnvironmentVariables(_applicationContext, settings, out var profilerEnvironmentVariables))
            {
                return 1;
            }

            AnsiConsole.WriteLine("Running: {0} {1}", program, arguments);

            if (Program.CallbackForTests != null)
            {
                Program.CallbackForTests(program, arguments, profilerEnvironmentVariables);
                return 0;
            }

            var processInfo = Utils.GetProcessStartInfo(program, Environment.CurrentDirectory, profilerEnvironmentVariables);
            if (!string.IsNullOrEmpty(arguments))
            {
                processInfo.Arguments = arguments;
            }

            return Utils.RunProcess(processInfo, _applicationContext.TokenSource.Token);
        }

        public override ValidationResult Validate(CommandContext context, RunSettings settings)
        {
            var runValidation = RunHelper.Validate(context, settings);
            return !runValidation.Successful ? runValidation : base.Validate(context, settings);
        }
    }
}
