// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Datadog.FleetInstaller;
using Datadog.FleetInstaller.Commands;

// TEMP: For easy local testing
// var extraArgs = new[] { "--home-path", @"C:\repos\dd-trace-dotnet-2\artifacts\monitoring-home" };
// args = ["install", ..extraArgs];
// args = ["reinstall", ..extraArgs];
// args = ["uninstall-version", ..extraArgs];
// args = ["uninstall-product"];
// args = [];

var rootCommand = new CommandWithExamples(CommandWithExamples.Command, "Windows SSI fleet-installer command line tool");

var builder = new CommandLineBuilder(rootCommand)
    .UseHelp()
    .UseVersionOption()
    .UseCustomErrorReporting()
    .CancelOnProcessTermination();

rootCommand.AddExample("""
                       install-version --home-path "C:\datadog\versions\3.9.0"
                       """);
rootCommand.AddExample("""
                       uninstall-version --home-path "C:\datadog\versions\3.9.0"
                       """);
rootCommand.AddExample("""
                       enable-iis-instrumentation --home-path "C:\datadog\versions\3.9.0"
                       """);
rootCommand.AddExample("""
                       remove-iis-instrumentation"
                       """);
rootCommand.AddExample("""
                       available-commands"
                       """);

rootCommand.AddCommand(new InstallVersionCommand());
rootCommand.AddCommand(new UninstallVersionCommand());
rootCommand.AddCommand(new EnableIisInstrumentationCommand());
rootCommand.AddCommand(new RemoveIisInstrumentationCommand());
rootCommand.AddCommand(new AvailableCommandsCommand(rootCommand));

return builder.Build().Invoke(args);

#pragma warning disable SA1649 // File name should match first type name
internal static class CustomErrorHandling
{
    public static CommandLineBuilder UseCustomErrorReporting(this CommandLineBuilder builder)
    {
        builder.AddMiddleware(
            async (context, next) =>
            {
                if (context.ParseResult.Errors.Count > 0)
                {
                    context.InvocationResult = new ParseErrorInvocationResult();
                }
                else
                {
                    await next(context).ConfigureAwait(true);
                }
            },
            (MiddlewareOrder)1000); // To match MiddlewareOrderInternal.ParseErrorReporting

        return builder;
    }

    /// <summary>
    /// A custom parse error reporter that prints the error message but does not print the help text.
    /// </summary>
    internal sealed class ParseErrorInvocationResult : IInvocationResult
    {
        public void Apply(InvocationContext context)
        {
            var log = Log.Instance;
            foreach (var error in context.ParseResult.Errors)
            {
                log.WriteError(error.Message);
            }

            context.ExitCode = 1;
        }
    }
}
