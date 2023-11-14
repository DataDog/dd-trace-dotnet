// <copyright file="AnalyzeInstrumentationErrorsCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.InstrumentedAssemblyGenerator;
using Datadog.InstrumentedAssemblyVerification;
using Datadog.Trace.Configuration.Telemetry;
using Spectre.Console;

namespace Datadog.Trace.Tools.Runner;

internal class AnalyzeInstrumentationErrorsCommand : CommandWithExamples
{
    private readonly Option<string> _processNameOption = new("--process-name", "Sets the process name.");
    private readonly Option<int?> _pidOption = new("--pid", "Sets the process ID.");
    private readonly Option<string> _logDirectoryOption = new("--log-path", "Sets the instrumentation log folder path.");
    private readonly Option<string> _originalAssembliesOption = new("--original-assemblies", "Sets if the original assemblies has copied during the app running.");
    private readonly Option<string> _assembliesToVerifyOption = new("--assemblies-to-verify", "Specify assembly names to verify separated by ';'.");

    public AnalyzeInstrumentationErrorsCommand()
        : base("analyze-instrumentation", "Analyze instrumentation errors")
    {
        AddOption(_processNameOption);
        AddOption(_pidOption);
        AddOption(_logDirectoryOption);
        AddOption(_originalAssembliesOption);
        AddOption(_assembliesToVerifyOption);

        AddExample("dd-trace analyze-instrumentation --process-name dotnet");
        AddExample("dd-trace analyze-instrumentation --pid 12345");
        AddExample(@"dd-trace analyze-instrumentation --log-path ""C:\ProgramData\Datadog .NET Tracer\logs\""");

        this.SetHandler(Execute);
    }

    private void Execute(InvocationContext context)
    {
        var processName = _processNameOption.GetValue(context);
        var pid = _pidOption.GetValue(context);
        var logDirectory = _logDirectoryOption.GetValue(context);

        var process = $"'{processName ?? "na"}'";
        if (pid != null)
        {
            process += ", pid: " + pid;
        }

        if (!string.IsNullOrEmpty(logDirectory))
        {
            process += ", provided log path is: " + logDirectory;
        }

        AnsiConsole.WriteLine("Running instrumentation error analysis on process: " + process);

        var tracerLogDirectory = !string.IsNullOrEmpty(logDirectory) ? logDirectory : GetLogDirectory(pid);
        AnsiConsole.WriteLine($"Tracer log directory is: \"{tracerLogDirectory ?? "null"}\"");
        if (!Directory.Exists(tracerLogDirectory))
        {
            Utils.WriteError("Tracer log directory does not exist.");
            context.ExitCode = -1;
            return;
        }

        var processLogDir = GetProcessInstrumentationVerificationLogDirectory(tracerLogDirectory, processName, pid);
        AnsiConsole.WriteLine($"Instrumentation verification output directory is: \"{processLogDir ?? "null"}\"");
        if (!Directory.Exists(processLogDir))
        {
            Utils.WriteError("Instrumentation verification output directory does not exist.");
            if (pid == null && string.IsNullOrEmpty(processName) && string.IsNullOrEmpty(logDirectory))
            {
                Utils.WriteError("Please provide either process name, process ID or instrumentation verification logs directory path");
            }

            context.ExitCode = -1;
            return;
        }

        bool hasOriginalAssemblies = false;
        var originalAssemblies = _originalAssembliesOption.GetValue(context);
        if (!string.IsNullOrEmpty(originalAssemblies))
        {
            hasOriginalAssemblies = bool.TryParse(originalAssemblies, out hasOriginalAssemblies);
        }

        string[] modulesToVerify = null;
        var assembliesToVerify = _assembliesToVerifyOption.GetValue(context);
        if (!string.IsNullOrEmpty(assembliesToVerify))
        {
            modulesToVerify = assembliesToVerify.Split(';', StringSplitOptions.RemoveEmptyEntries);
        }

        var generatorArgs = new AssemblyGeneratorArgs(processLogDir, copyOriginalModulesToDisk: hasOriginalAssemblies, modulesToVerify: modulesToVerify);

        var exportedModulesPathsAndMethods = InstrumentedAssemblyGeneration.Generate(generatorArgs);

        bool allVerificationsPassed = true;

        foreach (var instrumentedAssembly in exportedModulesPathsAndMethods)
        {
            var result = new VerificationsRunner(
                instrumentedAssembly.InstrumentedAssemblyPath,
                instrumentedAssembly.OriginalAssemblyPath,
                instrumentedAssembly.ModifiedMethods.Select(m => (m.TypeFullName, m.MethodAndArgumentsName)).ToList()).Run();
            AnsiConsole.WriteLine($"Verification for {instrumentedAssembly.InstrumentedAssemblyPath} {(result.IsValid ? "passed." : "failed: " + result.FailureReason)}");
            allVerificationsPassed = allVerificationsPassed && result.IsValid;
        }

        try
        {
            foreach (var instrumented in exportedModulesPathsAndMethods)
            {
                foreach (var method in instrumented.ModifiedMethods)
                {
                    AnsiConsole.WriteLine("--------------------------------------------------------");
                    AnsiConsole.WriteLine($"Method {method.MethodAndArgumentsName} has been instrumented.");
                    AnsiConsole.WriteLine($"Instrumented IL Code:{Environment.NewLine}{method.Instructions}");
                    AnsiConsole.WriteLine($"Instrumented Decompiled Code:{Environment.NewLine}{method.DecompiledCode.Value}");
                }
            }
        }
        catch (Exception e)
        {
            AnsiConsole.WriteLine($"Error while generating the instrumentation analysis report: {e}");
            context.ExitCode = -1;
            return;
        }

        context.ExitCode = allVerificationsPassed ? 0 : -1;
    }

    /// <param name="tracerLogDir">Tracer logs directory</param>
    /// <param name="processName">Process name if exist</param>
    /// <param name="pid">process id if exist</param>
    /// <returns>e.g. C:\ProgramData\Datadog .NET Tracer\logs\InstrumentationVerification\dotnet_12345_dd-mm-yyyy_hh-mm-ss or C:\ProgramData\Datadog-APM\logs\DotNet\dotnet_12345_dd-mm-yyyy_hh-mm-ss</returns>
    private string GetProcessInstrumentationVerificationLogDirectory(string tracerLogDir, string processName, int? pid)
    {
        var instrumentationVerificationLogs = Path.Combine(tracerLogDir, InstrumentedAssemblyGeneratorConsts.InstrumentedAssemblyGeneratorLogsFolder);
        if (!Directory.Exists(instrumentationVerificationLogs))
        {
            return null;
        }

        List<DirectoryInfo> dirs = Directory.EnumerateDirectories(instrumentationVerificationLogs).Select(d => new DirectoryInfo(d)).ToList();
        if (dirs.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(processName) && pid == null)
        {
            DirectoryInfo dir = null;
            if (dirs.Count > 1)
            {
                AnsiConsole.WriteLine($"There is more than one directory in {instrumentationVerificationLogs}, taking the last modified one");
                dir = dirs.OrderByDescending(di => di.LastWriteTime).First();
            }
            else
            {
                dir = dirs.FirstOrDefault();
            }

            return dir?.FullName;
        }

        var processNamePattern = string.IsNullOrEmpty(processName) ? "[A-Za-z0-9.]*" : $"({processName})";
        var pidPattern = pid == null ? "\\d+" : pid.ToString();
        var pattern = $"^{processNamePattern}(_){pidPattern}(_)[0-9-_]+$";
        var candidates = dirs.Where(d => Regex.IsMatch(d.Name, pattern)).ToList();
        if (candidates.Count == 1)
        {
            return candidates[0].FullName;
        }

        if (candidates.Count == 0)
        {
            AnsiConsole.WriteLine($"No directory was found matching pattern {pattern}, make sure {pid} is right");
            return null;
        }

        AnsiConsole.WriteLine("There is more than one directory that match the argument, taking the last modified directory");
        return candidates.OrderByDescending(dir => dir.LastWriteTime).First().FullName;
    }

    private string GetLogDirectory(int? pid)
    {
        string logDirectory = null;
        if (pid != null)
        {
            logDirectory = ProcessConfiguration.GetProcessLogDirectory(pid.Value);
        }

        return logDirectory ?? Logging.DatadogLoggingFactory.GetLogDirectory(NullConfigurationTelemetry.Instance);
    }
}
