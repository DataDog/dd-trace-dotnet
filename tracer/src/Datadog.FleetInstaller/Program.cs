// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Datadog.FleetInstaller.Commands;

// TEMP: For easy local testing
// var extraArgs = new[] { "--home-path", @"C:\repos\dd-trace-dotnet-2\artifacts\monitoring-home" };
// args = ["install", ..extraArgs];
// args = ["reinstall", ..extraArgs];
// args = ["uninstall-version", ..extraArgs];
// args = ["uninstall-product"];
// args = [];

var rootCommand = new CommandWithExamples(CommandWithExamples.Command);

var builder = new CommandLineBuilder(rootCommand)
    .UseHelp()
    .UseParseErrorReporting()
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

rootCommand.AddCommand(new InstallVersionCommand());
rootCommand.AddCommand(new UninstallVersionCommand());
rootCommand.AddCommand(new EnableIisInstrumentationCommand());
rootCommand.AddCommand(new RemoveIisInstrumentation());

return builder.Build().Invoke(args);
