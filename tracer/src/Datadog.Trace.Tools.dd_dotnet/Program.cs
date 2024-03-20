// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.dd_dotnet;

internal class Program
{
    internal static Action<string, string, Dictionary<string, string>?>? CallbackForTests { get; set; }

    internal static int Main(string[] args)
    {
        // Disable QUIC support. It requires reflection (and thus increases the final size of the binary)
        AppContext.SetSwitch("System.Net.SocketsHttpHandler.Http3Support", false);

        IEnumerable<HelpSectionDelegate> GetLayout(HelpContext context)
        {
            yield return HelpBuilder.Default.SynopsisSection();
            yield return HelpBuilder.Default.CommandUsageSection();

            if (context.Command is CommandWithExamples command && command.Examples.Count > 0)
            {
                yield return CommandWithExamples.ExamplesSection();
            }

            yield return HelpBuilder.Default.CommandArgumentsSection();
            yield return HelpBuilder.Default.OptionsSection();
            yield return HelpBuilder.Default.SubcommandsSection();
            yield return HelpBuilder.Default.AdditionalArgumentsSection();
        }

        var localizationResources = new CustomLocalizationResources();

        var rootCommand = new CommandWithExamples(CommandWithExamples.Command);

        var builder = new CommandLineBuilder(rootCommand)
            .UseLocalizationResources(localizationResources)
            .UseHelp()
            .UseParseErrorReporting()
            .CancelOnProcessTermination();

        builder.UseHelpBuilder(
            _ =>
            {
                var helpBuilder = new HelpBuilder(localizationResources);
                helpBuilder.CustomizeLayout(GetLayout);
                return helpBuilder;
            });

        rootCommand.AddExample("run --dd-env prod -- myApp --argument-for-my-app");
        rootCommand.AddExample("check process <pid>");
        rootCommand.AddExample("check iis <website>");

        var checkCommand = new Command("check");
        builder.Command.AddCommand(checkCommand);

        checkCommand.AddCommand(new CheckProcessCommand());
        checkCommand.AddCommand(new CheckAgentCommand());

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            checkCommand.AddCommand(new CheckIisCommand());
        }

        builder.Command.AddCommand(new RunCommand());
        builder.Command.AddCommand(new CreatedumpCommand());

        var parser = builder.Build();

        var parseResult = parser.Parse(args);

        try
        {
            return parseResult.Invoke();
        }
        catch (Exception ex)
        {
            Utils.WriteException(ex);
            return 1;
        }
    }
}
