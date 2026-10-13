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
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal abstract class CoverageEventHandler
{
    private readonly AsyncLocal<CoverageContextContainer?> _asyncContext = new(OnAsyncContextChanged);
    private readonly CoverageContextContainer _globalContainer = new(bufferKind: ModuleValue.BufferKind.GlobalFallback);
    private readonly ContextDiagnostics _contextDiagnostics = new();
    private readonly Action<IReadOnlyList<ModuleValue>> _completeDeferredSession;
    private readonly Action _recordContextDisposed;

    protected CoverageEventHandler()
    {
        _completeDeferredSession = CompleteDeferredSession;
        // Cache the instance delegate once; closed contexts may retain it briefly while an inherited
        // ExecutionContext finishes, so allocating a new delegate for every test would be unnecessary churn.
        _recordContextDisposed = _contextDiagnostics.RecordDisposed;
    }

    public CoverageContextContainer? Container => _asyncContext.Value;

    public CoverageContextContainer GlobalContainer => _globalContainer;

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
            ThrowHelper.ThrowInvalidOperationException("The coverage session handle belongs to another handler.");
        }

        var context = handle.Context;
        if (ReferenceEquals(_asyncContext.Value, context))
        {
            _asyncContext.Value = null;
        }

        if (!context.TryCloseAndGetModules(out var modules))
        {
            return null;
        }

        _contextDiagnostics.RecordClosed();
        var deferCompletion = false;
        try
        {
            var sessionEndData = OnSessionFinished(context, modules, out deferCompletion);
            if (context.State is MetricTags.CIVisibilityTestFramework telemetryTestingFramework)
            {
                TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageFinished(telemetryTestingFramework, MetricTags.CIVisibilityCoverageLibrary.Custom);
            }

            return sessionEndData;
        }
        finally
        {
            var admissionTransferred = false;
            try
            {
                if (deferCompletion)
                {
                    // Keep the session admission open until every inherited ExecutionContext has stopped
                    // using its cached probe pointer and the final counters have been merged.
                    admissionTransferred = true;
                    context.DisposeWhenExecutionContextsAreInactive(_completeDeferredSession, _recordContextDisposed, handle.Admission);
                }
                else
                {
                    context.DisposeWhenExecutionContextsAreInactive(null, _recordContextDisposed);
                }
            }
            finally
            {
                if (!admissionTransferred)
                {
                    handle.Admission.Release();
                }
            }
        }
    }

    public void AbortSession(CoverageSessionHandle handle, GlobalCoverageFailureReason reason)
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
            context.DisposeWhenExecutionContextsAreInactive(null, _recordContextDisposed, handle.Admission);
        }
        catch
        {
            // Abort is a structural no-throw cleanup path. The original functional exception wins.
        }
    }

    public void MarkProbeDataIncomplete(GlobalCoverageFailureReason reason)
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

    protected void LogContextDiagnostics(long merged)
        => _contextDiagnostics.Log(merged);

    protected abstract void OnSessionStart(CoverageContextContainer context);

    protected abstract object? OnSessionFinished(
        CoverageContextContainer context,
        IReadOnlyList<ModuleValue> modules,
        out bool deferCompletion);

    protected virtual void OnDeferredSessionFinished(IReadOnlyList<ModuleValue> modules)
    {
    }

    private static void OnAsyncContextChanged(AsyncLocalValueChangedArgs<CoverageContextContainer?> args)
    {
        if (ReferenceEquals(args.PreviousValue, args.CurrentValue))
        {
            return;
        }

        // Instrumented methods cache raw counter pointers for the duration of the method. Track active
        // ExecutionContexts here so session cleanup never adds synchronization to the probe hot path.
        args.PreviousValue?.OnExecutionContextExited();
        args.CurrentValue?.OnExecutionContextEntered();
    }

    private void CompleteDeferredSession(IReadOnlyList<ModuleValue> modules)
        => OnDeferredSessionFinished(modules);

    private sealed class ContextDiagnostics
    {
        private long _started;
        private long _closed;
        private long _disposed;

        public void RecordStarted() => Interlocked.Increment(ref _started);

        public void RecordClosed() => Interlocked.Increment(ref _closed);

        public void RecordDisposed() => Interlocked.Increment(ref _disposed);

        public void Log(long merged)
        {
            TestOptimization.Instance.Log.Debug<int, long, long, long, long>(
                "Global coverage context diagnostics: pid={ProcessId}, started={Started}, closed={Closed}, disposed={Disposed}, merged={Merged}.",
                DomainMetadata.Instance.ProcessId,
                Interlocked.Read(ref _started),
                Interlocked.Read(ref _closed),
                Interlocked.Read(ref _disposed),
                merged);
        }
    }
}
