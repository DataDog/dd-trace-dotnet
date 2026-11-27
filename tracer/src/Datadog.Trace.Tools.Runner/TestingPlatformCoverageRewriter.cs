// <copyright file="TestingPlatformCoverageRewriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
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

        var coverageSettings = new CoverageSettings(configurationElement: null, tracerHome, settings);
        var logger = new RunnerCoverageLogger();
        var assemblies = 0;
        foreach (var folder in folders)
        {
            assemblies += CoverageFolderProcessor.ProcessFolder(folder, SearchOption.TopDirectoryOnly, coverageSettings, logger);
        }

        Log.Information<int, int>("TestingPlatformCoverageRewriter: {Assemblies} assemblies processed in {Folders} folders.", assemblies, folders.Count);
        if (assemblies == 0)
        {
            reason = "No test assembly with symbols (.pdb) was found in the output folders.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private sealed class RunnerCoverageLogger : ICollectorLogger
    {
        public void Error(string? text) => Log.Error("TestingPlatformCoverageRewriter: {Message}", text);

        public void Error(Exception exception) => Log.Error(exception, "TestingPlatformCoverageRewriter: Error rewriting the test assemblies.");

        public void Error(Exception exception, string? text) => Log.Error(exception, "TestingPlatformCoverageRewriter: {Message}", text);

        public void Warning(string? text) => Log.Warning("TestingPlatformCoverageRewriter: {Message}", text);

        public void Debug(string? text) => Log.Debug("TestingPlatformCoverageRewriter: {Message}", text);
    }
}
