// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using Datadog.AutoInstrumentation.Generator.Cli.Commands;

var jsonOption = new Option<bool>("--json") { Description = "Output structured JSON instead of plain text", Recursive = true };

var rootCommand = new RootCommand("Datadog Auto-Instrumentation Generator CLI");
rootCommand.Options.Add(jsonOption);
rootCommand.Add(new GenerateCommand(jsonOption));
rootCommand.Add(new InspectCommand(jsonOption));
return rootCommand.Parse(args).Invoke();
