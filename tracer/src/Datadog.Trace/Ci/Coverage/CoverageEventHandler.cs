// <copyright file="CoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.Ci.Telemetry;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Ci.Coverage;

internal abstract class CoverageEventHandler
{
    private readonly AsyncLocal<CoverageContextContainer?> _asyncContext = new();
    private readonly CoverageContextContainer _globalContainer = new();
    private readonly CoverageContextDiagnostics _contextDiagnostics = new();

    protected CoverageEventHandler(CoverageModuleValueStrategy? moduleValueStrategy = null)
    {
        ModuleValueStrategy = moduleValueStrategy ?? CoverageModuleValueStrategy.Production;
    }

    internal CoverageContextContainer? Container => _asyncContext.Value;

    internal CoverageContextContainer GlobalContainer => _globalContainer;

    internal CoverageModuleValueStrategy ModuleValueStrategy { get; }

    internal CoverageContextDiagnosticSnapshot ContextDiagnostics => _contextDiagnostics.GetSnapshot();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CoverageSessionHandle StartSession(string? testingFramework = null)
    {
        CoverageContextAdmission? admission = null;
        CoverageContextContainer? context = null;
        var transferred = false;
        try
        {
            if (!TryBeginSessionStartAdmission(out admission))
            {
                return CoverageSessionHandle.Invalid;
            }

            var telemetryTestingFramework = TelemetryHelper.GetTelemetryTestingFrameworkEnum(testingFramework);
            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageStarted(telemetryTestingFramework, MetricTags.CIVisibilityCoverageLibrary.Custom);
            context = CreateContext(telemetryTestingFramework);
            OnSessionStart(context);
            InstallContext(context);
            admission.CommitInstalled();

            var handle = new CoverageSessionHandle(this, context, admission);
            _contextDiagnostics.RecordStarted();
            transferred = true;
            return handle;
        }
        finally
        {
            if (!transferred)
            {
                if (context is not null)
                {
                    if (ReferenceEquals(_asyncContext.Value, context))
                    {
                        _asyncContext.Value = null;
                    }

                    context.Dispose();
                }

                admission?.FailStart(GlobalCoverageFailureReason.StartFailed);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? EndSession(CoverageSessionHandle? handle)
    {
        if (handle is null || !handle.IsValid)
        {
            return null;
        }

        if (!ReferenceEquals(handle.Owner, this))
        {
            throw new InvalidOperationException("The coverage session handle belongs to another handler.");
        }

        var context = handle.Context!;
        if (ReferenceEquals(_asyncContext.Value, context))
        {
            _asyncContext.Value = null;
        }

        if (!context.TryCloseAndGetModules(out var modules))
        {
            return null;
        }

        _contextDiagnostics.RecordClosed();
        try
        {
            var sessionEndData = OnSessionFinished(context, modules);
            if (context.State is MetricTags.CIVisibilityTestFramework telemetryTestingFramework)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageFinished(telemetryTestingFramework, MetricTags.CIVisibilityCoverageLibrary.Custom);
            }

            return sessionEndData;
        }
        finally
        {
            try
            {
                context.Dispose();
                _contextDiagnostics.RecordDisposed();
            }
            finally
            {
                handle.Admission.Release();
            }
        }
    }

    internal void AbortSession(CoverageSessionHandle handle, GlobalCoverageFailureReason reason)
    {
        try
        {
            if (!ReferenceEquals(handle.Owner, this) || handle.Context is not { } context)
            {
                return;
            }

            if (ReferenceEquals(_asyncContext.Value, context))
            {
                _asyncContext.Value = null;
            }

            if (!context.TryCloseAndGetModules(out _))
            {
                return;
            }

            _contextDiagnostics.RecordClosed();
            MarkGlobalCoverageIncomplete(reason);
            try
            {
                context.Dispose();
                _contextDiagnostics.RecordDisposed();
            }
            finally
            {
                handle.Admission.Release();
            }
        }
        catch
        {
            // Abort is a structural no-throw cleanup path. The original functional exception wins.
        }
    }

    internal void MarkProbeDataIncomplete(GlobalCoverageFailureReason reason)
    {
        try
        {
            MarkGlobalCoverageIncomplete(reason);
        }
        catch
        {
            // Never replace the probe exception with completeness bookkeeping.
        }
    }

    protected virtual bool TryBeginSessionStartAdmission(out CoverageContextAdmission admission)
    {
        admission = CoverageContextAdmission.Noop;
        return true;
    }

    protected virtual CoverageContextContainer CreateContext(object? state) => new(state);

    protected virtual void InstallContext(CoverageContextContainer context) => _asyncContext.Value = context;

    protected virtual void MarkGlobalCoverageIncomplete(GlobalCoverageFailureReason reason)
    {
    }

    protected abstract void OnSessionStart(CoverageContextContainer context);

    protected abstract object? OnSessionFinished(CoverageContextContainer context, IReadOnlyList<ModuleValue> modules);
}
