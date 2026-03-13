// <copyright file="Program.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.CommandLine;
using Datadog.AutoInstrumentation.Generator.Cli.Commands;

var rootCommand = new RootCommand("Datadog Auto-Instrumentation Generator CLI");
rootCommand.Add(new GenerateCommand());
return rootCommand.Parse(args).Invoke();
