// <copyright file="AnalyzeCriticalErrorCommand.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.InstrumentedAssemblyGenerator;
using Datadog.InstrumentedAssemblyVerification;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner;

internal class AnalyzeCriticalErrorCommand : AsyncCommand<AnalyzeCriticalErrorSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeCriticalErrorSettings settings)
    {
        AnsiConsole.Record();
        AnsiConsole.WriteLine("Running critical error analysis on process " + settings.Pid + ". Error was in method " + "." + settings.Method);

        var module = GetMainModule(settings);
        if (module == null)
        {
            Utils.WriteError("No module found.");
            return -1;
        }

        var logDirectory = GetLogDirectory(settings.LogDirectory);
        if (logDirectory == null)
        {
            Utils.WriteError("Log directory does not exist.");
            return -1;
        }

        var baseFolder = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Path.Combine(logDirectory, $"{module}_{settings.Pid}") : Path.Combine(logDirectory, module);

        if (!Directory.Exists(baseFolder))
        {
            Utils.WriteError("Log directory has not found.");
            return -1;
        }

        var generatorArgs = new AssemblyGeneratorArgs(baseFolder, modulesToVerify: new[] { module }, methodsToVerify: settings.Method?.Split(","));

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
                    AnsiConsole.WriteLine($"Method {method.FullName}  has been instrumented.");
                    AnsiConsole.WriteLine($"Instrumented IL Code: {method.Instructions}");
                    AnsiConsole.WriteLine($"Instrumented Decompiled Code: {method.DecompiledCode}");
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

    private string GetLogDirectory(string logDirectory)
    {
        var dir = logDirectory ?? "DefaultLogDirectory";
        return Directory.Exists(dir) ? dir : null;
    }

    private string GetMainModule(AnalyzeCriticalErrorSettings settings)
    {
        if (!string.IsNullOrEmpty(settings.Module))
        {
            return null;
        }

        if (settings.Pid == null)
        {
            return null;
        }

        var process = ProcessInfo.GetProcessInfo(settings.Pid.Value);

        if (process == null)
        {
            Utils.WriteError("Could not fetch information about target process. Make sure to run the command from an elevated prompt, and check that the pid is correct.");
            return null;
        }

        return process.MainModule != null ? Path.GetFileName(process.MainModule) : null;
    }
}
