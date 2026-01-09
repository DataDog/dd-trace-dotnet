// <copyright file="AvailableCommandsCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Threading.Tasks;

namespace Datadog.FleetInstaller.Commands;

/// <summary>
/// Installs a new version of the .NET Tracer. Could be the first version, or simply a new version
/// </summary>
internal sealed class AvailableCommandsCommand : CommandBase
{
    private const string Command = "available-commands";
    private const string CommandDescription = "Prints the list of available commands in this version of the .NET Fleet Installer to stdout";
    private readonly Command _rootCommand;

    public AvailableCommandsCommand(Command rootCommand)
        : base(Command, CommandDescription)
    {
        _rootCommand = rootCommand;
        this.SetHandler(ExecuteAsync);
    }

    public Task ExecuteAsync(InvocationContext context)
    {
        // We write directly to stdout, instead of the logger

        var result = Execute(Console.Out, _rootCommand);

        context.ExitCode = (int)result;
        return Task.CompletedTask;
    }

    // Internal for testing
#pragma warning disable SA1005 // Comment must not begin with a space (required for lang injection)
    internal static ReturnCode Execute(TextWriter writer, Command rootCommand)
    {
        //language=JSON
        var json = $$"""
                     {
                       "commands": [
                         {{string.Join(",\n    ", GetCommandsJson(rootCommand))}}
                       ]
                     }
                     """;

        writer.WriteLine(json);
        return ReturnCode.Success;

        static IEnumerable<string> GetCommandsJson(Command rootCommand)
        {
            foreach (var command in rootCommand.Subcommands)
            {
                if (command.IsHidden)
                {
                    continue;
                }

                //language=JSON
                yield return $$"""{ "name": "{{command.Name}}", "options": [{{string.Join(", ", GetOptionsJson(command))}}] }""";
            }
        }

        static IEnumerable<string> GetOptionsJson(Command command)
        {
            foreach (var option in command.Options)
            {
                //language=JSON
                yield return $$"""{ "name": "{{option.Name}}"}""";
            }
        }
    }
}
