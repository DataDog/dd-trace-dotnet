// <copyright file="TestingPlatformCoverageRewriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Rewrites the test assemblies of a prebuilt Microsoft.Testing.Platform run for per-test code coverage, like the
/// VSTest <c>DatadogCoverage</c> data collector does, and puts the original files back when it is disposed.
/// </summary>
internal sealed class TestingPlatformCoverageRewriter : IDisposable
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformCoverageRewriter));

    private readonly List<(string OriginalPath, string? BackupPath)> _files = [];
    private readonly string _backupDirectory = Path.Combine(Path.GetTempPath(), "dd-trace-mtp-backup-" + Path.GetRandomFileName());
    private int _disposed;

    private TestingPlatformCoverageRewriter()
    {
    }

    /// <summary>
    /// Gets the number of assemblies the rewrite processed, including the ones that already had coverage.
    /// </summary>
    public int AssembliesProcessed { get; private set; }

    /// <summary>
    /// Rewrites the test assemblies of a <c>dotnet test</c> command line when it is safe to: the Microsoft.Testing.Platform command runs
    /// the assemblies as they are in their output folders, which only holds without a build and for options that are understood.
    /// </summary>
    /// <param name="program">The dotnet executable.</param>
    /// <param name="arguments">The arguments of the command, with the response files expanded.</param>
    /// <param name="workingDirectory">The directory the command runs in.</param>
    /// <param name="tracerHome">The tracer home with the Datadog.Trace assemblies to copy next to the test assemblies.</param>
    /// <param name="settings">The settings that configure the rewrite.</param>
    /// <param name="rewriter">The rewriter to dispose after the run, or null when nothing was rewritten.</param>
    /// <param name="reason">Why nothing was rewritten, or null.</param>
    /// <returns>True when the assemblies were rewritten.</returns>
    public static bool TryRewrite(string program, IReadOnlyList<string> arguments, string workingDirectory, string? tracerHome, TestOptimizationSettings settings, out TestingPlatformCoverageRewriter? rewriter, out string? reason)
    {
        rewriter = null;
        if (StringUtil.IsNullOrEmpty(tracerHome))
        {
            reason = "The tracer home is not set.";
            return false;
        }

        var commandLine = TestingPlatformCommandLine.Parse(arguments, workingDirectory);
        if (commandLine.UnsupportedOption is { } unsupportedOption)
        {
            reason = $"The '{unsupportedOption}' option is not supported.";
            return false;
        }

        if (!commandLine.RunsPrebuiltAssemblies)
        {
            reason = "'dotnet test' builds the test projects, and the build replaces the rewritten assemblies. Use '--no-build' to get per-test code coverage.";
            return false;
        }

        if (!TestingPlatformModuleLocator.TryGetOutputFolders(program, commandLine, workingDirectory, out var folders))
        {
            reason = "The output folders of the test assemblies could not be found.";
            return false;
        }

        var candidate = new TestingPlatformCoverageRewriter();
        try
        {
            candidate.Rewrite(folders, tracerHome!, settings);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "TestingPlatformCoverageRewriter: Error rewriting the test assemblies.");
            candidate.Dispose();
            reason = "The test assemblies could not be rewritten.";
            return false;
        }

        if (candidate.AssembliesProcessed == 0)
        {
            candidate.Dispose();
            reason = "There are no test assemblies with symbols to rewrite.";
            return false;
        }

        // The run can end without the caller reaching its cleanup, for example when the runner is asked to terminate.
        AppDomain.CurrentDomain.ProcessExit += candidate.OnProcessExit;
        rewriter = candidate;
        reason = null;
        return true;
    }

    private static void Restore(string originalPath, string? backupPath)
    {
        // The test process may still hold the file for a moment after it was stopped.
        const int maxAttempts = 25;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (backupPath is not null)
                {
                    File.Copy(backupPath, originalPath, overwrite: true);
                }
                else if (File.Exists(originalPath))
                {
                    File.Delete(originalPath);
                }

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "TestingPlatformCoverageRewriter: Unable to restore {Path}.", originalPath);
                return;
            }
        }
    }

    private static bool HasSymbols(string assemblyPath)
        => File.Exists(Path.ChangeExtension(assemblyPath, ".pdb")) || File.Exists(Path.ChangeExtension(assemblyPath, ".PDB"));

    /// <summary>
    /// Puts the original files back and removes the files that were added.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        foreach (var (originalPath, backupPath) in _files)
        {
            Restore(originalPath, backupPath);
        }

        try
        {
            if (Directory.Exists(_backupDirectory))
            {
                Directory.Delete(_backupDirectory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "TestingPlatformCoverageRewriter: Error deleting the backup folder.");
        }
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    private void Rewrite(IReadOnlyList<string> folders, string tracerHome, TestOptimizationSettings settings)
    {
        var coverageSettings = new CoverageSettings(null, tracerHome, settings);
        var logger = new RunnerCoverageLogger();
        foreach (var folder in folders)
        {
            // The files are backed up before they can change, so an interrupted rewrite is also undone.
            BackUp(folder);
            AssembliesProcessed += CoverageFolderProcessor.Process(folder, SearchOption.TopDirectoryOnly, coverageSettings, logger);
            Log.Debug("TestingPlatformCoverageRewriter: Processed the assemblies in {Folder}.", folder);
        }
    }

    private void BackUp(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                (CoverageFolderProcessor.HasAssemblyExtension(file) && HasSymbols(file)) ||
                name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(name), typeof(Tracer).Assembly.GetName().Name, StringComparison.OrdinalIgnoreCase))
            {
                BackUpFile(file);
            }
        }

        // The tracer assemblies are copied next to the test assemblies: the files that didn't exist are removed afterwards.
        var tracerName = typeof(Tracer).Assembly.GetName().Name;
        foreach (var extension in new[] { ".dll", ".pdb" })
        {
            var path = Path.Combine(folder, tracerName + extension);
            if (!File.Exists(path))
            {
                _files.Add((path, null));
            }
        }
    }

    private void BackUpFile(string path)
    {
        Directory.CreateDirectory(_backupDirectory);
        var backupPath = Path.Combine(_backupDirectory, _files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        File.Copy(path, backupPath, overwrite: true);
        _files.Add((path, backupPath));
    }
}
