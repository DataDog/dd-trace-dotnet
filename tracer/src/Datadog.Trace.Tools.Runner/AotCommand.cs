// <copyright file="AotCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner
{
    internal class AotCommand : Command<AotSettings>
    {
        public override int Execute(CommandContext context, AotSettings settings)
        {
            try
            {
                Aot.AotProcessor.ProcessFolder(settings.InputFolder, settings.OutputFolder);
            }
            catch
            {
                return 1;
            }

            return 0;
        }
    }
}
