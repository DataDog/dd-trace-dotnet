// <copyright file="TestingPlatformCoverageRewriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Coverage.Collector;
using Datadog.Trace.Logging;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Tools.Runner;

/// <summary>
/// Rewrites the test assemblies of a prebuilt Microsoft.Testing.Platform run for per-test code coverage, like the
/// VSTest <c>DatadogCoverage</c> data collector does, and puts the original files back when it is disposed.
/// </summary>
/// <remarks>
/// Each output folder holds a lock file for the duration of the run, so two runs can't rewrite the same folder at once.
/// The lock file also records what was changed. When a run is killed before it could restore the files, the next run
/// finds that record and restores the files that are still as the killed run left them.
/// </remarks>
internal sealed class TestingPlatformCoverageRewriter : IDisposable
{
    internal const string LockFileName = ".dd-trace-coverage-rewrite.lock";

    private const int RestoreAttempts = 25;
    private const string BackupDirectoryPrefix = "dd-trace-mtp-backup-";
    private const string CompletedMarker = "DONE";

    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(TestingPlatformCoverageRewriter));
    private static readonly string[] TracerAssemblyExtensions = [".dll", ".pdb"];

    private readonly object _gate = new();
    private readonly List<OutputFolder> _folders = [];
    private readonly string _backupDirectory = Path.Combine(Path.GetTempPath(), BackupDirectoryPrefix + Path.GetRandomFileName());
    private TimeSpan _restoreRetryDelay = TimeSpan.FromMilliseconds(200);
    private bool _disposed;
    private bool _rewriteCompleted;
    private int _backupCount;

    private TestingPlatformCoverageRewriter()
    {
    }

    /// <summary>
    /// Gets the number of assemblies the rewrite looked at, including the ones that were skipped or already had coverage.
    /// </summary>
    public int AssembliesProcessed { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the rewrite changed any file.
    /// </summary>
    public bool ChangedFiles { get; private set; }

    /// <summary>
    /// Gets the folder with the copies of the original files.
    /// </summary>
    [TestingOnly]
    internal string BackupDirectory => _backupDirectory;

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
        if (StringUtil.IsNullOrEmpty(tracerHome) || !Directory.Exists(tracerHome))
        {
            reason = "The tracer home is not set or doesn't exist.";
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

        // The run can end without the caller reaching its cleanup, for example when the runner is asked to terminate.
        // Subscribing before anything is touched also undoes a rewrite that is interrupted half way.
        AppDomain.CurrentDomain.ProcessExit += candidate.OnProcessExit;
        try
        {
            candidate.Rewrite(folders, tracerHome, settings);
        }
        catch (OutputFolderInUseException ex)
        {
            Log.Warning(ex, "TestingPlatformCoverageRewriter: The output folder is in use.");
            candidate.Dispose();
            reason = "Another dd-trace run is rewriting the same output folder.";
            return false;
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

        // The assemblies may all have been skipped, for example because they were already rewritten or are excluded: that is not a failure.
        Log.Information<int, bool>("TestingPlatformCoverageRewriter: {Assemblies} assembly(ies) looked at, files changed: {Changed}.", candidate.AssembliesProcessed, candidate.ChangedFiles);
        rewriter = candidate;
        reason = null;
        return true;
    }

    /// <summary>
    /// Sets the wait between two attempts to restore a file that is in use.
    /// </summary>
    /// <param name="delay">The wait.</param>
    [TestingOnly]
    internal void SetRestoreRetryDelay(TimeSpan delay) => _restoreRetryDelay = delay;

    /// <summary>
    /// Puts the original files back and removes the files that were added. The backup is kept when a file could not be restored.
    /// </summary>
    public void Dispose()
    {
        // A second caller, such as the process exit handler while the run is cleaning up, waits for the first one to finish.
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;

            var failed = 0;
            foreach (var folder in _folders)
            {
                var folderFailed = 0;
                foreach (var file in folder.Files)
                {
                    if (!Restore(file, _rewriteCompleted, _restoreRetryDelay))
                    {
                        folderFailed++;
                    }
                }

                failed += folderFailed;
                folder.Release(deleteLockFile: folderFailed == 0);
            }

            if (failed > 0)
            {
                var message = $"The original test assemblies could not be restored for {failed} file(s), possibly because a test process still holds them. The next 'dd-trace ci run' over the same output folders restores them, or rebuild the test projects. Copies of the originals are in '{_backupDirectory}'.";
                Log.Warning<string?>("TestingPlatformCoverageRewriter: {Message}", message);
                Utils.WriteWarning(message);
                return;
            }

            DeleteBackupDirectory(_backupDirectory);
        }
    }

    /// <summary>
    /// Ends the run the way a killed process does: the lock is released and nothing is restored.
    /// </summary>
    [TestingOnly]
    internal void Abandon()
    {
        lock (_gate)
        {
            _disposed = true;
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            foreach (var folder in _folders)
            {
                folder.Release(deleteLockFile: false);
            }
        }
    }

    [TestingOnly]
    internal void SimulateProcessExit() => OnProcessExit(null, EventArgs.Empty);

    private static FileState GetState(string path)
    {
        var info = new FileInfo(path);
        return info.Exists ? new FileState(info.Length, info.LastWriteTimeUtc.Ticks) : default;
    }

    private static bool HasSymbols(string assemblyPath)
        => File.Exists(Path.ChangeExtension(assemblyPath, ".pdb")) || File.Exists(Path.ChangeExtension(assemblyPath, ".PDB"));

    private static void DeleteBackupDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && Path.GetFileName(directory).StartsWith(BackupDirectoryPrefix, StringComparison.Ordinal))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "TestingPlatformCoverageRewriter: Error deleting the backup folder.");
        }
    }

    private static bool Restore(BackedUpFile file, bool rewriteCompleted, TimeSpan retryDelay)
    {
        // A file that is not in the state the rewrite left it in was changed by something else since: leave it alone.
        // When the rewrite didn't complete its final state is unknown, so the original is always put back.
        var current = GetState(file.OriginalPath);
        if (rewriteCompleted && current != file.AfterRewrite)
        {
            Log.Debug("TestingPlatformCoverageRewriter: {Path} changed after the rewrite, it is not restored.", file.OriginalPath);
            return true;
        }

        if (file.BackupPath is not null && !File.Exists(file.BackupPath))
        {
            Log.Warning("TestingPlatformCoverageRewriter: The copy of {Path} is gone, it can't be restored.", file.OriginalPath);
            return false;
        }

        // The test process may still hold the file for a moment after it was stopped.
        for (var attempt = 1; attempt <= RestoreAttempts; attempt++)
        {
            try
            {
                if (file.BackupPath is not null)
                {
                    if (current != file.BeforeRewrite)
                    {
                        File.Copy(file.BackupPath, file.OriginalPath, overwrite: true);
                    }
                }
                else if (File.Exists(file.OriginalPath))
                {
                    File.Delete(file.OriginalPath);
                }

                return true;
            }
            catch (IOException) when (attempt < RestoreAttempts)
            {
                Thread.Sleep(retryDelay);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warning(ex, "TestingPlatformCoverageRewriter: Unable to restore {Path}.", file.OriginalPath);
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "TestingPlatformCoverageRewriter: Error restoring {Path}.", file.OriginalPath);
                return false;
            }
        }

        return false;
    }

    private static void WriteManifest(FileStream stream, IReadOnlyList<BackedUpFile> files, bool completed)
    {
        stream.SetLength(0);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true);
        foreach (var file in files)
        {
            writer.WriteLine(
                string.Join(
                    "\t",
                    file.BackupPath is null ? "A" : "B",
                    file.OriginalPath,
                    file.BackupPath ?? string.Empty,
                    file.BeforeRewrite.Length.ToString(CultureInfo.InvariantCulture),
                    file.BeforeRewrite.LastWriteTicks.ToString(CultureInfo.InvariantCulture),
                    file.AfterRewrite.Length.ToString(CultureInfo.InvariantCulture),
                    file.AfterRewrite.LastWriteTicks.ToString(CultureInfo.InvariantCulture)));
        }

        if (completed)
        {
            writer.WriteLine(CompletedMarker);
        }

        writer.Flush();
        stream.Flush(flushToDisk: true);
    }

    private static List<BackedUpFile>? ReadCompletedManifest(FileStream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: false, 1024, leaveOpen: true);
        var files = new List<BackedUpFile>();
        var completed = false;
        while (reader.ReadLine() is { } line)
        {
            if (line == CompletedMarker)
            {
                completed = true;
                continue;
            }

            var parts = line.Split('\t');
            if (parts.Length != 7 ||
                !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var beforeLength) ||
                !long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var beforeTicks) ||
                !long.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var afterLength) ||
                !long.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var afterTicks))
            {
                return null;
            }

            files.Add(new BackedUpFile(parts[1], parts[0] == "B" ? parts[2] : null, new FileState(beforeLength, beforeTicks), new FileState(afterLength, afterTicks)));
        }

        // A record without the completed marker belongs to a run that was killed while it was still rewriting: what
        // it left can't be told apart from a rebuild, so it is not touched.
        return completed ? files : null;
    }

    private void OnProcessExit(object? sender, EventArgs e) => Dispose();

    private void Rewrite(IReadOnlyList<string> folders, string tracerHome, TestOptimizationSettings settings)
    {
        var coverageSettings = new CoverageSettings(null, tracerHome, settings);
        var logger = new RunnerCoverageLogger();
        foreach (var path in folders)
        {
            var folder = Lock(path);
            _folders.Add(folder);
            BackUp(folder);
            WriteManifest(folder.Lock, folder.Files, completed: false);
            AssembliesProcessed += CoverageFolderProcessor.Process(path, SearchOption.TopDirectoryOnly, coverageSettings, logger);
            Log.Debug("TestingPlatformCoverageRewriter: Processed the assemblies in {Folder}.", path);
        }

        foreach (var folder in _folders)
        {
            for (var i = 0; i < folder.Files.Count; i++)
            {
                var after = GetState(folder.Files[i].OriginalPath);
                ChangedFiles |= after != folder.Files[i].BeforeRewrite;
                folder.Files[i] = folder.Files[i] with { AfterRewrite = after };
            }

            WriteManifest(folder.Lock, folder.Files, completed: true);
        }

        _rewriteCompleted = true;
    }

    private OutputFolder Lock(string folder)
    {
        // Held until the run is over. A second run over the same folder fails here instead of taking the files this run
        // already rewrote for the originals. The operating system releases the lock if this process dies.
        var lockPath = Path.Combine(folder, LockFileName);
        FileStream stream;
        try
        {
            stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1);
        }
        catch (IOException ex)
        {
            throw new OutputFolderInUseException(folder, ex);
        }

        try
        {
            if (stream.Length > 0)
            {
                RecoverFromKilledRun(stream, folder);
            }
        }
        catch
        {
            stream.Dispose();
            throw;
        }

        return new OutputFolder(folder, lockPath, stream);
    }

    private void RecoverFromKilledRun(FileStream stream, string folder)
    {
        var files = ReadCompletedManifest(stream);
        stream.SetLength(0);
        if (files is null)
        {
            Log.Debug("TestingPlatformCoverageRewriter: The record of an earlier run in {Folder} is not complete, nothing is restored.", folder);
            return;
        }

        Log.Information("TestingPlatformCoverageRewriter: Restoring the files an earlier run left rewritten in {Folder}.", folder);
        foreach (var file in files)
        {
            Restore(file, rewriteCompleted: true, _restoreRetryDelay);
        }

        var backedUp = files.Find(file => file.BackupPath is not null);
        if (backedUp.BackupPath is { } backupPath && Path.GetDirectoryName(backupPath) is { } backupDirectory)
        {
            DeleteBackupDirectory(backupDirectory);
        }
    }

    private void BackUp(OutputFolder folder)
    {
        var tracerName = typeof(Tracer).Assembly.GetName().Name;
        foreach (var file in Directory.EnumerateFiles(folder.Path, "*.*", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileName(file);
            if (file.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) ||
                (CoverageFolderProcessor.HasAssemblyExtension(file) && HasSymbols(file)) ||
                name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(Path.GetFileNameWithoutExtension(name), tracerName, StringComparison.OrdinalIgnoreCase))
            {
                BackUpFile(folder, file);
            }
        }

        // The tracer assemblies are copied next to the test assemblies: the files that didn't exist are removed afterwards.
        foreach (var extension in TracerAssemblyExtensions)
        {
            var path = Path.Combine(folder.Path, tracerName + extension);
            if (!File.Exists(path))
            {
                folder.Files.Add(new BackedUpFile(path, null, default, default));
            }
        }
    }

    private void BackUpFile(OutputFolder folder, string path)
    {
        Directory.CreateDirectory(_backupDirectory);
        var backupPath = Path.Combine(_backupDirectory, (++_backupCount).ToString(CultureInfo.InvariantCulture));
        File.Copy(path, backupPath, overwrite: true);
        folder.Files.Add(new BackedUpFile(path, backupPath, GetState(path), default));
    }

    private readonly record struct FileState(long Length, long LastWriteTicks);

    private readonly record struct BackedUpFile(string OriginalPath, string? BackupPath, FileState BeforeRewrite, FileState AfterRewrite);

    private sealed class OutputFolder
    {
        public OutputFolder(string path, string lockPath, FileStream lockStream)
        {
            Path = path;
            LockPath = lockPath;
            Lock = lockStream;
        }

        public string Path { get; }

        public string LockPath { get; }

        public FileStream Lock { get; }

        public List<BackedUpFile> Files { get; } = [];

        public void Release(bool deleteLockFile)
        {
            try
            {
                if (deleteLockFile)
                {
                    // An empty lock file is left behind when the record is cleared, and removed when it can be.
                    Lock.SetLength(0);
                }

                Lock.Dispose();
                if (deleteLockFile)
                {
                    File.Delete(LockPath);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "TestingPlatformCoverageRewriter: Error releasing the lock of {Folder}.", Path);
            }
        }
    }

    private sealed class OutputFolderInUseException : IOException
    {
        public OutputFolderInUseException(string folder, Exception innerException)
            : base($"The output folder '{folder}' is being rewritten by another run.", innerException)
        {
        }
    }
}
