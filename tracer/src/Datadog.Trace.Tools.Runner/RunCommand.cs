// <copyright file="RunCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCommand : Command<RunSettings>
    {
        public RunCommand(ApplicationContext applicationContext)
        {
            ApplicationContext = applicationContext;
        }

        protected ApplicationContext ApplicationContext { get; }

        public override int Execute(CommandContext context, RunSettings settings)
        {
            return RunHelper.Execute(ApplicationContext, context, settings);
        }

        public override ValidationResult Validate(CommandContext context, RunSettings settings)
        {
            var runValidation = RunHelper.Validate(context, settings);
            return !runValidation.Successful ? runValidation : base.Validate(context, settings);
        }
    }
}
