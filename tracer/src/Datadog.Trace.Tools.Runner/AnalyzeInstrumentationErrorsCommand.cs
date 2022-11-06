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
using Datadog.Trace.Configuration;
using Datadog.Trace.Tools.Runner.Checks;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Datadog.Trace.Tools.Runner;

internal class AnalyzeInstrumentationErrorsCommand : AsyncCommand<AnalyzeInstrumentationErrorsSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AnalyzeInstrumentationErrorsSettings settings)
    {
        AnsiConsole.Record();
        var process = $"'{settings.ProcessName ?? "na"}'";
        if (settings.Pid != null)
        {
            process += ", pid: " + settings.Pid;
        }

        if (!string.IsNullOrEmpty(settings.LogDirectory))
        {
            process += ", log path is: " + settings.LogDirectory;
        }

        AnsiConsole.WriteLine("Running instrumentation error analysis on process: " + process);

        var logDirectory = GetLogDirectory(settings.LogDirectory, settings.Pid);
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

        var generatorArgs = new AssemblyGeneratorArgs(processLogDir, modulesToVerify: null);

        var exportedModulesPathsAndMethods = InstrumentedAssemblyGeneration.Generate(generatorArgs);

        bool allVerificationsPassed = true;

        foreach (var instrumentedAssembly in exportedModulesPathsAndMethods)
        {
            var result = new VerificationsRunner(instrumentedAssembly.InstrumentedAssemblyPath, instrumentedAssembly.OriginalAssemblyPath, instrumentedAssembly.InstrumentedMethods.Select(m => (m.MethodName, m.TypeName)).ToList()).Run();
            AnsiConsole.WriteLine($"Verification for {instrumentedAssembly.InstrumentedAssemblyPath} {(result.IsValid ? "passed." : "failed: " + result.FailureReason)}");
            allVerificationsPassed = allVerificationsPassed && result.IsValid;
        }

        try
        {
            foreach (var instrumented in exportedModulesPathsAndMethods)
            {
                foreach (var method in instrumented.InstrumentedMethods)
                {
                    AnsiConsole.WriteLine("--------------------------------------------------------");
                    AnsiConsole.WriteLine($"Method {method.FullName} has been instrumented.");
                    AnsiConsole.WriteLine($"Instrumented IL Code:{Environment.NewLine}{method.Instructions}");
                    AnsiConsole.WriteLine($"Instrumented Decompiled Code:{Environment.NewLine}{method.DecompiledCode.Value}");
                }
            }

            var checkProcessReport = AnsiConsole.ExportText();
            await Task.Yield();
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

    private string GetLogDirectory(string logDirectory, int? pid)
    {
        if (!string.IsNullOrEmpty(logDirectory))
        {
            return logDirectory;
        }

        if (pid != null)
        {
            var process = ProcessInfo.GetProcessInfo(pid.Value);
            logDirectory = process?.Configuration?.GetString(ConfigurationKeys.LogDirectory);
            if (logDirectory == null)
            {
#pragma warning disable 618 // ProfilerLogPath is deprecated but still supported
                var nativeLogFile = process?.Configuration?.GetString(ConfigurationKeys.ProfilerLogPath);
#pragma warning restore 618
                if (!string.IsNullOrEmpty(nativeLogFile))
                {
                    logDirectory = Path.GetDirectoryName(nativeLogFile);
                }
            }
        }

        return logDirectory ?? Logging.DatadogLoggingFactory.GetLogDirectory();
    }
}
