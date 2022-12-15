// <copyright file="AnalyzeInstrumentationErrorsCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.InstrumentedAssemblyGenerator;
using Datadog.InstrumentedAssemblyVerification;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner;

internal class AnalyzeInstrumentationErrorsCommand : Command<AnalyzeInstrumentationErrorsSettings>
{
    public override int Execute(CommandContext context, AnalyzeInstrumentationErrorsSettings settings)
    {
        var process = $"'{settings.ProcessName ?? "na"}'";
        if (settings.Pid != null)
        {
            process += ", pid: " + settings.Pid;
        }

        if (!string.IsNullOrEmpty(settings.LogDirectory))
        {
            process += ", provided log path is: " + settings.LogDirectory;
        }

        AnsiConsole.WriteLine("Running instrumentation error analysis on process: " + process);

        var tracerLogDirectory = !string.IsNullOrEmpty(settings.LogDirectory) ? settings.LogDirectory : GetLogDirectory(settings.Pid);
        AnsiConsole.WriteLine($"Tracer log directory is: \"{tracerLogDirectory ?? "null"}\"");
        if (!Directory.Exists(tracerLogDirectory))
        {
            Utils.WriteError("Tracer log directory does not exist.");
            return -1;
        }

        var processLogDir = GetProcessInstrumentationVerificationLogDirectory(tracerLogDirectory, settings);
        AnsiConsole.WriteLine($"Instrumentation verification output directory is: \"{processLogDir ?? "null"}\"");
        if (!Directory.Exists(processLogDir))
        {
            Utils.WriteError("Instrumentation verification output directory does not exist.");
            if (settings.Pid == null && string.IsNullOrEmpty(settings.ProcessName) && string.IsNullOrEmpty(settings.LogDirectory))
            {
                Utils.WriteError("Please provide either process name, process ID or instrumentation verification logs directory path");
            }

            return -1;
        }

        var generatorArgs = new AssemblyGeneratorArgs(processLogDir, modulesToVerify: null);

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
            return -1;
        }

        return allVerificationsPassed ? 0 : -1;
    }

    /// <param name="tracerLogDir">Tracer logs directory</param>
    /// <returns>e.g. C:\ProgramData\Datadog .NET Tracer\logs\InstrumentationVerification\dotnet_12345_dd-mm-yyyy_hh-mm-ss</returns>
    private string GetProcessInstrumentationVerificationLogDirectory(string tracerLogDir, AnalyzeInstrumentationErrorsSettings settings)
    {
        var instrumentationVerificationLogs = Path.Combine(tracerLogDir, InstrumentedAssemblyGeneratorConsts.InstrumentedAssemblyGeneratorLogsFolder);
        if (!Directory.Exists(instrumentationVerificationLogs))
        {
            return null;
        }

        var dirs = Directory.EnumerateDirectories(instrumentationVerificationLogs).Select(d => new DirectoryInfo(d)).ToList();
        if (dirs.Count == 0)
        {
            return null;
        }

        if (string.IsNullOrEmpty(settings.ProcessName) && settings.Pid == null)
        {
            DirectoryInfo dir = null;
            if (dirs.Count > 1)
            {
                AnsiConsole.WriteLine($"There is more than one directory in {instrumentationVerificationLogs}, taking the last modified one");
                dir = dirs.OrderByDescending(dir => dir.LastWriteTime).First();
            }
            else
            {
                dir = dirs[0];
            }

            return dir.FullName;
        }

        var processName = string.IsNullOrEmpty(settings.ProcessName) ? "[A-Z0-9]" : $"({settings.ProcessName})";
        var pid = settings.Pid == null ? "\\d+" : settings.Pid.ToString();
        var pattern = $"^{processName}(_){pid}(_)[0-9-_]+$";

        var candidates = dirs.Where(d => Regex.IsMatch(d.Name, pattern)).ToList();
        if (candidates.Count == 1)
        {
            return candidates[0].FullName;
        }

        AnsiConsole.WriteLine("There is more than one directory that match the argument, taking the last modified directory");
        return candidates.OrderByDescending(dir => dir.LastWriteTime).FirstOrDefault()?.FullName;
    }

    private string GetLogDirectory(int? pid)
    {
        string logDirectory = null;
        if (pid != null)
        {
            var process = ProcessInfo.GetProcessInfo(pid.Value);
            logDirectory = process?.GetProcessLogDirectory();
        }

        return logDirectory ?? Logging.DatadogLoggingFactory.GetLogDirectory();
    }
}
