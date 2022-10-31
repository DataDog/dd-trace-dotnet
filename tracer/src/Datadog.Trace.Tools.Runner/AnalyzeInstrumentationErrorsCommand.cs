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
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner;

internal class AnalyzeInstrumentationErrorsCommand : AsyncCommand<AnalyzeInstrumentationErrorsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeInstrumentationErrorsSettings settings)
    {
        AnsiConsole.Record();
        AnsiConsole.WriteLine("Running instrumentation error analysis on process " + (settings.Pid != null ? settings.Pid : "NA"));
        if (!string.IsNullOrEmpty(settings.Method))
        {
            AnsiConsole.WriteLine("Error was in method " + "." + settings.Method);
        }

        var logDirectory = GetLogDirectory(settings.LogDirectory);
        if (logDirectory == null)
        {
            Utils.WriteError("Log directory does not exist.");
            return -1;
        }

        var processLogDir = GetProcessLogDirectory(logDirectory, settings);

        if (!Directory.Exists(processLogDir))
        {
            Utils.WriteError("Log directory has not found.");
            return -1;
        }

        var generatorArgs = new AssemblyGeneratorArgs(processLogDir, modulesToVerify: settings.Module == null ? null : new[] { settings.Module });

        var exportedModulesPathsAndMethods = InstrumentedAssemblyGeneration.Generate(generatorArgs);

        bool allVerificationsPassed = true;

        foreach (var ((instrumentedModulePath, originalModulePath), methods) in exportedModulesPathsAndMethods)
        {
            var result = new VerificationsRunner(instrumentedModulePath, originalModulePath, methods.Select(m => (m.MethodName, m.TypeName)).ToList()).Run();
            AnsiConsole.WriteLine($"Verification for {instrumentedModulePath} {(result.IsValid ? "passed." : "failed: " + result.FailureReason)}");
            allVerificationsPassed = allVerificationsPassed && result.IsValid;
        }

        try
        {
            foreach (var export in exportedModulesPathsAndMethods)
            {
                foreach (var method in export.methods)
                {
                    AnsiConsole.WriteLine("--------------------------------------------------------");
                    AnsiConsole.WriteLine($"Method {method.FullName} has been instrumented.");
                    AnsiConsole.WriteLine($"Instrumented IL Code:{Environment.NewLine}{method.Instructions}");
                    AnsiConsole.WriteLine($"Instrumented Decompiled Code:{Environment.NewLine}{method.DecompiledCode.Value}");
                }
            }

            var checkProcessReport = AnsiConsole.ExportText();
            await TelemetryReporter.UploadAsLogMessage(checkProcessReport).ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (Exception e)
        {
            AnsiConsole.WriteLine($"Error while uploading the report: {e}");
            return -1;
        }

        return allVerificationsPassed ? 0 : -1;
    }

    private string GetProcessLogDirectory(string baseLogDir, AnalyzeInstrumentationErrorsSettings settings)
    {
        var dirs = Directory.EnumerateDirectories(baseLogDir).Select(d => new DirectoryInfo(d)).ToList();
        if (dirs.Count == 0)
        {
            return null;
        }

        if (dirs.Count == 1)
        {
            if (string.IsNullOrEmpty(settings.ProcessName) && settings.Pid == null)
            {
                return dirs[0].FullName;
            }
        }

        if (string.IsNullOrEmpty(settings.ProcessName) && settings.Pid == null)
        {
            return null;
        }

        var processName = string.IsNullOrEmpty(settings.ProcessName) ? "[A-Z0-9]" : $"({settings.ProcessName})";
        var pid = settings.Pid == null ? "\\d+" : settings.Pid.ToString();
        var pattern = $"^{processName}(_){pid}(_)[0-9-_]+$";
        return dirs.SingleOrDefault(d => Regex.IsMatch(d.Name, pattern))?.FullName;
    }

    private string GetLogDirectory(string logDirectory)
    {
        var dir = logDirectory ?? @"C:\ProgramData\Datadog-APM\logs\DotNet\InstrumentationVerification";
        return Directory.Exists(dir) ? dir : null;
    }
}
