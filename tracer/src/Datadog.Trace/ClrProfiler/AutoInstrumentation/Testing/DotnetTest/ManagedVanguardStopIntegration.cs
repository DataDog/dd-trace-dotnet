// <copyright file="ManagedVanguardStopIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
    internal static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, in CallTargetState state)
        where TTarget : IManagedVanguardProxy
    {
        if (instance.GetOutputCoverageFiles() is { } lstFiles)
        {
            foreach (var file in lstFiles.Distinct())
            {
                if (file is null)
                {
                    continue;
                }

                if (!Path.GetExtension(file).Equals(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var shouldBackfill = DotnetCommon.TryGetCoverageBackfillDataForCurrentProcess(out var backfillData);
                if (!ExternalCoverageXmlBackfill.TryProcess(file, shouldBackfill ? backfillData : null, shouldBackfill, out var coverageResult))
                {
                    if (shouldBackfill)
                    {
                        DotnetCommon.Log.Warning("MicrosoftCodeCoverage: XML report could not be backfilled, so no stale coverage percentage will be sent.");
                        TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                    }

                    continue;
                }

                {
                    DotnetCommon.Log.Information("MicrosoftCodeCoverage.Percentage: {Value}", coverageResult.Percentage);

                    // Extract session variables (from out of process sessions)
                    var context = Tracer.Instance.TracerManager.SpanContextPropagator.Extract(
                        EnvironmentHelpers.GetEnvironmentVariables(),
                        new DictionaryGetterAndSetter(DictionaryGetterAndSetter.EnvironmentVariableKeyProcessor));

                    if (context.SpanContext is { } sessionContext)
                    {
                        try
                        {
                            var name = $"session_{sessionContext.SpanId}";
                            Common.Log.Debug("DataCollector.Enabling IPC client: {Name}", name);
                            using var ipcClient = new IpcClient(name);
                            Common.Log.Debug("DataCollector.Sending session code coverage: {Value}", coverageResult.Percentage);
                            if (!ipcClient.TrySendMessage(
                                    new SessionCodeCoverageMessage(
                                        CodeCoverageReportSource.MicrosoftCodeCoverage,
                                        coverageResult.Percentage,
                                        coverageResult.Backfilled,
                                        coverageResult.ExecutableLines,
                                        coverageResult.CoveredLines,
                                        coverageResult.Diagnostic)))
                            {
                                Common.Log.Warning("ManagedVanguardStopIntegration: Could not send Microsoft CodeCoverage IPC message.");
                                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
                                CoverageBackfillDataStore.RecordCoverageIpcFailure(nameof(CodeCoverageReportSource.MicrosoftCodeCoverage));
                            }
                        }
                        catch (Exception ex)
                        {
                            Common.Log.Error(ex, "Error enabling IPC client and sending coverage data");
                        }
                    }
                }
            }
        }

        return CallTargetReturn.GetDefault();
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
