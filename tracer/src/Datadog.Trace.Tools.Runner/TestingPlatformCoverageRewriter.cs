// <copyright file="TestingPlatformCoverageRewriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Rewrites the test assemblies for per-test code coverage before <c>dotnet test</c> runs them with the Microsoft.Testing.Platform command.
/// </summary>
/// <remarks>
/// The VSTest <c>DatadogCoverage</c> data collector rewrites the assemblies of the test output folders when the test session starts,
/// but Microsoft.Testing.Platform doesn't load data collectors, so the runner does the same rewrite before launching the command.
/// As with the data collector, the rewritten files stay in the output folders, and an assembly that was already rewritten is left as it is.
/// The rewrite is only done when the command runs assemblies that are already built: a build would replace the rewritten files.
/// </remarks>
internal static class TestingPlatformCoverageRewriter
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformCoverageRewriter));

    /// <summary>
    /// Rewrites the test assemblies that a <c>dotnet test</c> command runs with Microsoft.Testing.Platform.
    /// </summary>
    /// <param name="program">The dotnet executable, also used to evaluate the projects.</param>
    /// <param name="arguments">The arguments of the command, with response files already expanded.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="tracerHome">The tracer home folder, which holds the <c>Datadog.Trace.dll</c> copied next to the rewritten assemblies.</param>
    /// <param name="settings">The Test Optimization settings of the run.</param>
    /// <param name="reason">Why the assemblies weren't rewritten, for the warning shown to the user.</param>
    /// <returns>True when at least one test assembly was rewritten or was already rewritten.</returns>
    public static bool TryRewrite(string program, IReadOnlyList<string> arguments, string workingDirectory, string? tracerHome, TestOptimizationSettings settings, out string reason)
    {
        var commandLine = TestingPlatformCommandLine.Parse(arguments, workingDirectory);
        if (!commandLine.RunsPrebuiltAssemblies)
        {
            reason = "'dotnet test' builds the test projects, and the build would replace the rewritten assemblies. Build first and use '--no-build', or select the assemblies with '--test-modules'.";
            return false;
        }

        if (commandLine.UnsupportedOption is { } unsupportedOption)
        {
            reason = $"The output folders of the test projects can't be found when the '{unsupportedOption}' option is used.";
            return false;
        }

        if (StringUtil.IsNullOrEmpty(tracerHome) || !Directory.Exists(tracerHome))
        {
            reason = "The tracer home folder can't be found.";
            return false;
        }

        if (!TestingPlatformModuleLocator.TryGetOutputFolders(program, commandLine, workingDirectory, out var folders))
        {
            reason = "The output folders of the test projects can't be found.";
            return false;
        }

        // A project or target framework that wasn't built for this configuration has no output folder; 'dotnet test' reports it.
        var existingFolders = folders.Where(Directory.Exists).ToList();
        if (existingFolders.Count == 0)
        {
            reason = "The output folders of the test projects don't exist. Build the test projects before running 'dotnet test --no-build'.";
            return false;
        }

        Log.Debug<int>("TestingPlatformCoverageRewriter: Rewriting the test assemblies in {Folders} folders.", existingFolders.Count);
        var coverageSettings = new CoverageSettings(configurationElement: null, tracerHome, settings);
        var logger = new RunnerCoverageLogger();
        var assemblies = 0;
        try
        {
            foreach (var folder in existingFolders)
            {
                assemblies += CoverageFolderProcessor.ProcessFolder(folder, SearchOption.TopDirectoryOnly, coverageSettings, logger);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestingPlatformCoverageRewriter: Error rewriting the test assemblies.");
            reason = "Rewriting the test assemblies failed: " + ex.Message;
            return false;
        }

        Log.Information<int, int>("TestingPlatformCoverageRewriter: {Assemblies} assemblies processed in {Folders} folders.", assemblies, existingFolders.Count);
        if (assemblies == 0)
        {
            reason = "No test assembly with symbols (.pdb) was found in the output folders.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    // The messages of the coverage rewriter contain paths and names, so they are logged as a property, and each kind of message
    // has its own template to be told apart in telemetry (errors with an exception also carry its type and stack).
    private sealed class RunnerCoverageLogger : ICollectorLogger
    {
        public void Error(string? text) => Log.Error("TestingPlatformCoverageRewriter: Coverage rewriter error: {Message}", text);

        public void Error(Exception exception) => Log.Error(exception, "TestingPlatformCoverageRewriter: Coverage rewriter exception.");

        public void Error(Exception exception, string? text) => Log.Error(exception, "TestingPlatformCoverageRewriter: Coverage rewriter exception: {Message}", text);

        public void Warning(string? text) => Log.Warning("TestingPlatformCoverageRewriter: Coverage rewriter warning: {Message}", text);

        public void Debug(string? text) => Log.Debug("TestingPlatformCoverageRewriter: Coverage rewriter debug message: {Message}", text);
    }
}
