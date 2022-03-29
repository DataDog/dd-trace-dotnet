// <copyright file="RunCiCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class RunCiCommand : RunCommand
    {
        public RunCiCommand(ApplicationContext applicationContext)
            : base(applicationContext)
        {
        }

        public override int Execute(CommandContext context, RunSettings settings)
        {
            return Execute(context, settings, enableCiMode: true);
        }
    }
}
