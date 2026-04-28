// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using System.Linq;
using Datadog.AutoInstrumentation.Generator.Cli.Commands;
using Datadog.AutoInstrumentation.Generator.Cli.Output;

var jsonOption = new Option<bool>("--json") { Description = "Output structured JSON instead of plain text", Recursive = true };

var rootCommand = new RootCommand("Datadog Auto-Instrumentation Generator CLI");
rootCommand.Options.Add(jsonOption);
rootCommand.Add(new GenerateCommand(jsonOption));
rootCommand.Add(new InspectCommand(jsonOption));

var parseResult = rootCommand.Parse(args);

// Honor the --json contract for parser-level errors (unknown options, missing required
// arguments, invalid value conversions). Without this, System.CommandLine prints the
// default help/error text to stderr and JSON-mode callers get unparseable output.
if (parseResult.Errors.Count > 0 && parseResult.GetValue(jsonOption))
{
    var commandName = parseResult.CommandResult.Command.Name;
    if (commandName == rootCommand.Name)
    {
        commandName = string.Empty;
    }

    var message = "Error: " + string.Join(" ", parseResult.Errors.Select(e => e.Message));
    return OutputHelper.WriteError(jsonMode: true, commandName, ErrorCodes.InvalidArgument, message);
}

return parseResult.Invoke();
