// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using Datadog.FleetInstaller.Commands;

// TEMP: For easy local testing
var extraArgs = new[] { "--symlink-path", @"C:\datadog2", "--versioned-path", @"C:\repos\dd-trace-dotnet-2\artifacts\monitoring-home" };
string[] install = ["install", ..extraArgs];
string[] reinstall = ["reinstall", ..extraArgs];
string[] uninstall = ["uninstall-version", ..extraArgs];
string[] remove = ["uninstall-all", ..extraArgs];
args = remove;

var rootCommand = new CommandWithExamples(CommandWithExamples.Command);

var builder = new CommandLineBuilder(rootCommand)
    .UseHelp()
    .UseParseErrorReporting()
    .CancelOnProcessTermination();

rootCommand.AddExample("""
                       install --symlink-path "C:\datadog\stable" --versioned-path "C:\datadog\versions\3.9.0"
                       """);
rootCommand.AddExample("""
                       reinstall --symlink-path "C:\datadog\stable" --versioned-path "C:\datadog\versions\3.9.0"
                       """);
rootCommand.AddExample("""
                       uninstall-version --symlink-path "C:\datadog\stable" --versioned-path "C:\datadog\versions\3.9.0"
                       """);
rootCommand.AddExample("""
                       uninstall-all --symlink-path "C:\datadog\stable" --versioned-path "C:\datadog\versions\3.9.0"
                       """);

rootCommand.AddCommand(new InstallCommand());
rootCommand.AddCommand(new ReinstallCommand());
rootCommand.AddCommand(new UninstallVersionCommand());
rootCommand.AddCommand(new UninstallAllCommand());

builder.Build().Invoke(args);
