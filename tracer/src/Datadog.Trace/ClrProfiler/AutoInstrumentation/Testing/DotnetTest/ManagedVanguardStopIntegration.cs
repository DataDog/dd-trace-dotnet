// <copyright file="ManagedVanguardStopIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Coverage;
using Datadog.Trace.Ci.Coverage.Backfill;
using Datadog.Trace.Ci.Ipc;
using Datadog.Trace.Ci.Ipc.Messages;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.DotnetTest;

/// <summary>
/// System.Void Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard::Stop() calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "Microsoft.VisualStudio.TraceDataCollector",
    TypeName = "Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard",
    MethodName = "Stop",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = [],
    MinimumVersion = "15.0.0",
    MaximumVersion = "15.*.*",
    IntegrationName = DotnetCommon.DotnetTestIntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class ManagedVanguardStopIntegration
{
    private const int CoverageXmlRestoreMaxAttempts = 3;
    private const int CoverageXmlRestoreRetryDelayMilliseconds = 50;
    private static readonly StringComparer CoverageReportPathComparer = FrameworkDescription.Instance.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IManagedVanguardProxy
    {
        if (exception is not null)
        {
            Common.Log.Warning(exception, "ManagedVanguardStopIntegration: Microsoft CodeCoverage failed to stop, so no coverage percentage will be sent.");
            RecordMicrosoftCoverageIpcFailure();
            return CallTargetReturn.GetDefault();
        }

        try
        {
            var coverageFiles = GetXmlCoverageFiles(instance.GetOutputCoverageFiles());
            if (coverageFiles.Count == 0)
            {
                Common.Log.Warning("ManagedVanguardStopIntegration: Microsoft CodeCoverage did not provide any XML coverage report paths.");
                RecordMicrosoftCoverageIpcFailure();
                return CallTargetReturn.GetDefault();
            }

            if (!TryGetParentSessionSpanId(out var parentSessionSpanId))
            {
                Common.Log.Warning("ManagedVanguardStopIntegration: Could not find the parent test session context for Microsoft CodeCoverage IPC.");
                RecordMicrosoftCoverageIpcFailure();
                return CallTargetReturn.GetDefault();
            }

            var backfillData = CoverageBackfillData.Missing;
            var shouldBackfill = DotnetCommon.TryGetCoverageBackfillDataForCurrentProcess(parentSessionSpanId, out backfillData, out var unavailableAfterActualItrSkip);
            if (!shouldBackfill && unavailableAfterActualItrSkip)
            {
                DotnetCommon.Log.Warning("MicrosoftCodeCoverage: ITR skipped tests but backend coverage backfill data is unavailable, so no stale coverage percentage will be sent.");
                RecordMicrosoftCoverageIpcFailure(parentSessionSpanId);
                return CallTargetReturn.GetDefault();
            }

            var coverageResults = new List<ExternalCoverageXmlResult>(coverageFiles.Count);
            var processedCoverageFiles = new List<string>(coverageFiles.Count);
            var coverageReportBackups = shouldBackfill ? new List<CoverageXmlReportBackup>(coverageFiles.Count) : null;
            var validationStates = shouldBackfill ? new List<ExternalCoverageXmlBackfill.CoverageBackfillValidationState>(coverageFiles.Count) : null;
            foreach (var file in coverageFiles)
            {
                if (shouldBackfill)
                {
                    if (!TryReadCoverageXmlReportBackup(file, out var backup))
                    {
                        TryRestoreCoverageXmlReports(coverageReportBackups);
                        DotnetCommon.Log.Warning("MicrosoftCodeCoverage: XML report could not be backed up before ITR coverage backfill, so no stale coverage percentage will be sent.");
                        RecordMicrosoftCoverageIpcFailure();
                        return CallTargetReturn.GetDefault();
                    }

                    coverageReportBackups!.Add(backup);
                }

                var validationState = shouldBackfill ? new ExternalCoverageXmlBackfill.CoverageBackfillValidationState() : null;
                if (!ExternalCoverageXmlBackfill.TryProcessMicrosoft(file, shouldBackfill ? backfillData : null, shouldBackfill, validationState, out var coverageResult))
                {
                    TryRestoreCoverageXmlReports(coverageReportBackups);
                    DotnetCommon.Log.Warning("MicrosoftCodeCoverage: XML report could not be processed, so no partial coverage percentage will be sent.");
                    RecordMicrosoftCoverageIpcFailure();
                    return CallTargetReturn.GetDefault();
                }

                coverageResults.Add(coverageResult);
                processedCoverageFiles.Add(file);
                if (validationState is not null)
                {
                    validationStates!.Add(validationState);
                }
            }

            if (coverageResults.Count == 0)
            {
                Common.Log.Warning("ManagedVanguardStopIntegration: Microsoft CodeCoverage XML reports could not be processed, so no stale coverage percentage will be sent.");
                RecordMicrosoftCoverageIpcFailure();
                return CallTargetReturn.GetDefault();
            }

            var mergedValidationState = shouldBackfill ? new ExternalCoverageXmlBackfill.CoverageBackfillValidationState() : null;
            if (validationStates is not null)
            {
                foreach (var validationState in validationStates)
                {
                    mergedValidationState!.Merge(validationState);
                }
            }

            if (mergedValidationState is not null && !mergedValidationState.CanPublish())
            {
                TryRestoreCoverageXmlReports(coverageReportBackups);
                DotnetCommon.Log.Warning("MicrosoftCodeCoverage: XML reports could not be safely reconciled with backend ITR coverage, so no stale coverage percentage will be sent.");
                RecordMicrosoftCoverageIpcFailure();
                return CallTargetReturn.GetDefault();
            }

            if (!DotnetCommon.TryMergeCoverageXmlResults(coverageResults, processedCoverageFiles, mergedValidationState, out var mergedCoverageResult))
            {
                TryRestoreCoverageXmlReports(coverageReportBackups);
                DotnetCommon.Log.Warning("MicrosoftCodeCoverage: XML reports could not be aggregated, so no stale coverage percentage will be sent.");
                RecordMicrosoftCoverageIpcFailure();
                return CallTargetReturn.GetDefault();
            }

            var backfillValidated = mergedValidationState?.BackfillValidated == true;
            SendCoverageResult(parentSessionSpanId, mergedCoverageResult, processedCoverageFiles, backfillValidated);
        }
        catch (Exception ex)
        {
            Common.Log.Error(ex, "ManagedVanguardStopIntegration: Error processing Microsoft CodeCoverage XML report.");
            RecordMicrosoftCoverageIpcFailure();
        }

        return CallTargetReturn.GetDefault();
    }

    private static List<string> GetXmlCoverageFiles(IList<string>? files)
    {
        var coverageFiles = new List<string>();
        if (files is null)
        {
            return coverageFiles;
        }

        var seenFiles = new HashSet<string>(CoverageReportPathComparer);
        foreach (var file in files)
        {
            if (StringUtil.IsNullOrWhiteSpace(file))
            {
                continue;
            }

            try
            {
                var fullPath = Path.GetFullPath(file);
                if (Path.GetExtension(fullPath).Equals(".xml", StringComparison.OrdinalIgnoreCase) &&
                    seenFiles.Add(fullPath))
                {
                    coverageFiles.Add(fullPath);
                }
            }
            catch (Exception ex)
            {
                Common.Log.Debug(ex, "ManagedVanguardStopIntegration: Ignoring invalid Microsoft CodeCoverage report path: {Path}", file);
            }
        }

        return coverageFiles;
    }

    private static void SendCoverageResult(ulong sessionSpanId, ExternalCoverageXmlResult coverageResult, IReadOnlyList<string> coverageReportPaths, bool backfillValidated)
    {
        DotnetCommon.Log.Information("MicrosoftCodeCoverage.Percentage: {Value}", coverageResult.Percentage);
        var stableResultId = GetMicrosoftCoverageResultId(sessionSpanId, coverageReportPaths);
        var coverageResultId = CoverageBackfillDataStore.RecordCoverageIpcResult(
            TestOptimization.Instance,
            sessionSpanId,
            CodeCoverageReportSource.MicrosoftCodeCoverage,
            coverageResult.Percentage,
            coverageResult.Backfilled,
            coverageResult.ExecutableLines,
            coverageResult.CoveredLines,
            coverageResult.Diagnostic,
            resultId: stableResultId,
            backfillValidated: backfillValidated);

        try
        {
            var name = $"session_{sessionSpanId}";
            Common.Log.Debug("DataCollector.Enabling IPC client: {Name}", name);
            using var ipcClient = new IpcClient(name);
            Common.Log.Debug("DataCollector.Sending session code coverage: {Value}", coverageResult.Percentage);
            var sessionCodeCoverageMessage = DotnetCommon.CreateSessionCodeCoverageIpcMessage(
                CodeCoverageReportSource.MicrosoftCodeCoverage,
                coverageResult.Percentage,
                coverageResult.Backfilled,
                coverageResult.ExecutableLines,
                coverageResult.CoveredLines,
                coverageResult.Diagnostic,
                inlineResultId: stableResultId,
                persistedResultId: coverageResultId,
                backfillValidated: backfillValidated);
            DotnetCommon.TrySendSessionCodeCoverageIpcMessage(
                ipcClient,
                sessionCodeCoverageMessage,
                () =>
                {
                    Common.Log.Warning("ManagedVanguardStopIntegration: Could not send Microsoft CodeCoverage IPC message.");
                    RecordMicrosoftCoverageIpcFailure(sessionSpanId);
                });
        }
        catch (Exception ex)
        {
            Common.Log.Error(ex, "Error enabling IPC client and sending coverage data");
            RecordMicrosoftCoverageIpcFailure(sessionSpanId);
        }
    }

    private static string GetMicrosoftCoverageResultId(ulong sessionSpanId, IReadOnlyList<string> coverageReportPaths)
    {
        var normalizedPaths = new List<string>(coverageReportPaths.Count);
        foreach (var coverageReportPath in coverageReportPaths)
        {
            normalizedPaths.Add(Path.GetFullPath(coverageReportPath).Replace('\\', '/'));
        }

        normalizedPaths.Sort(CoverageReportPathComparer);
        var payload = Encoding.UTF8.GetBytes($"{CodeCoverageReportSource.MicrosoftCodeCoverage}|v1|{sessionSpanId}|{string.Join("|", normalizedPaths)}");
#if NET6_0_OR_GREATER
        var hash = SHA256.HashData(payload);
#else
        using System.Security.Cryptography.HashAlgorithm sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(payload);
#endif
        return $"microsoft-xml-{HexString.ToHexString(hash)}";
    }

    private static void RecordMicrosoftCoverageIpcFailure()
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
        if (TryGetParentSessionSpanId(out var sessionSpanId))
        {
            CoverageBackfillDataStore.RecordCoverageIpcFailure(sessionSpanId, nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
        }
    }

    private static void RecordMicrosoftCoverageIpcFailure(ulong sessionSpanId)
    {
        TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
        CoverageBackfillDataStore.RecordCoverageIpcFailure(sessionSpanId, nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
    }

    private static bool TryGetParentSessionSpanId(out ulong sessionSpanId)
    {
        var context = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
            EnvironmentHelpers.GetEnvironmentVariables(),
            new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

        if (context.SpanContext is { } sessionContext)
        {
            sessionSpanId = sessionContext.SpanId;
            return true;
        }

        sessionSpanId = 0;
        return false;
    }

    private static bool TryReadCoverageXmlReportBackup(string filePath, out CoverageXmlReportBackup backup)
    {
        try
        {
            backup = new CoverageXmlReportBackup(filePath, File.ReadAllBytes(filePath));
            return true;
        }
        catch (Exception ex)
        {
            Common.Log.Debug(ex, "ManagedVanguardStopIntegration: Could not read Microsoft CodeCoverage XML report backup: {Path}", filePath);
            backup = default;
            return false;
        }
    }

    private static void TryRestoreCoverageXmlReports(List<CoverageXmlReportBackup>? backups)
    {
        if (backups is null)
        {
            return;
        }

        foreach (var backup in backups)
        {
            try
            {
                RestoreCoverageXmlReport(backup);
            }
            catch (Exception ex)
            {
                Common.Log.Debug(ex, "ManagedVanguardStopIntegration: Could not restore Microsoft CodeCoverage XML report: {Path}", backup.FilePath);
            }
        }
    }

    private static void RestoreCoverageXmlReport(CoverageXmlReportBackup backup)
    {
        var fullPath = Path.GetFullPath(backup.FilePath);
        var directoryPath = Path.GetDirectoryName(fullPath);
        if (StringUtil.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        var fileName = Path.GetFileName(fullPath);
        var temporaryPath = Path.Combine(directoryPath!, $".{fileName}.{Guid.NewGuid():N}.restore.tmp");
        var replaceBackupPath = temporaryPath + ".bak";
        Exception? lastException = null;
        for (var attempt = 1; attempt <= CoverageXmlRestoreMaxAttempts; attempt++)
        {
            try
            {
                File.WriteAllBytes(temporaryPath, backup.Contents);
                if (File.Exists(fullPath))
                {
                    File.Replace(temporaryPath, fullPath, replaceBackupPath);
                    TryDeleteFile(replaceBackupPath);
                    return;
                }

                File.Move(temporaryPath, fullPath);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                TryDeleteFile(replaceBackupPath);
                if (attempt < CoverageXmlRestoreMaxAttempts)
                {
                    Thread.Sleep(CoverageXmlRestoreRetryDelayMilliseconds * attempt);
                }
            }
        }

        try
        {
            if (!File.Exists(temporaryPath))
            {
                File.WriteAllBytes(temporaryPath, backup.Contents);
            }
        }
        catch (Exception ex)
        {
            Common.Log.Debug(ex, "ManagedVanguardStopIntegration: Microsoft CodeCoverage XML restore backup could not be retained: {Path}", backup.FilePath);
        }

        Common.Log.Warning(lastException, "ManagedVanguardStopIntegration: Could not restore Microsoft CodeCoverage XML report: {Path}. Original bytes were retained at: {TemporaryPath}", backup.FilePath, temporaryPath);
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception)
        {
            // Best-effort cleanup only.
        }
    }

    private readonly struct CoverageXmlReportBackup
    {
        public CoverageXmlReportBackup(string filePath, byte[] contents)
        {
            FilePath = filePath;
            Contents = contents;
        }

        public string FilePath { get; }

        public byte[] Contents { get; }
    }

    /// <summary>
    /// DuckTyping interface for Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard
    /// </summary>
#pragma warning disable SA1201
    internal interface IManagedVanguardProxy : IDuckType
#pragma warning restore SA1201
    {
        /// <summary>
        /// Calls method: System.Collections.Generic.IList`1[System.String] Microsoft.VisualStudio.TraceCollector.VanguardCollector.ManagedVanguard::GetOutputCoverageFiles()
        /// </summary>
        IList<string>? GetOutputCoverageFiles();
    }
}
