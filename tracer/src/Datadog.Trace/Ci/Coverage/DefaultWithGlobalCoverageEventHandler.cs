// <copyright file="DefaultWithGlobalCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class DefaultWithGlobalCoverageEventHandler : DefaultCoverageEventHandler
{
    private readonly object _lifecycleGate = new();
    private readonly GlobalCoverageAccumulator _accumulator;
    private readonly GlobalCoverageOutputManager _outputManager;
    private int _inFlightStarts;
    private int _activeContexts;
    private int _inFlightSnapshots;
    private LifecycleState _state;
    private Action<bool>? _sealCompleted;
    private bool _sealStarted;
    private bool _sealRequested;
    private bool _sealedComplete;

    public DefaultWithGlobalCoverageEventHandler(
        GlobalCoverageAccumulatorLimits? limits = null,
        string? configuredOutputDirectory = null,
        Func<string>? runIdProvider = null)
    {
        _accumulator = new GlobalCoverageAccumulator(limits);
        _outputManager = new GlobalCoverageOutputManager(
            configuredOutputDirectory,
            Environment.CurrentDirectory,
            runIdProvider ?? (() => TestOptimization.Instance.RunId));
    }

    private enum AdmissionState
    {
        Starting,
        Active,
        Released,
    }

    private enum LifecycleState
    {
        Running,
        Completing,
        Sealed,
    }

    public GlobalCoverageSnapshotResult AcquireGlobalCoverageSnapshot()
    {
        var admission = new SnapshotAdmission(this);
        lock (_lifecycleGate)
        {
            if (_state != LifecycleState.Running)
            {
                return GlobalCoverageSnapshotResult.Suppressed(_accumulator.FailureReason);
            }

            _inFlightSnapshots++;
        }

        try
        {
            var result = _accumulator.AcquireSnapshot(GlobalContainer, admission.Release);
            if (result.Status != GlobalCoverageSnapshotStatus.Success)
            {
                admission.Release();
            }

            return result;
        }
        catch
        {
            admission.Release();
            throw;
        }
    }

    public bool TryCommit(GlobalCoverageSnapshot snapshot, Action action)
        => _accumulator.TryCommit(snapshot, action);

    public bool RegisterCollectorOutputDirectory(string directory)
    {
        var registered = _outputManager.RegisterCollectorAndFreeze(directory);
        if (!registered)
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
        }

        return registered;
    }

    public bool FinalizeAndSeal(Action<bool>? onCompleted = null)
    {
        var completeNow = false;
        bool? completed = null;
        lock (_lifecycleGate)
        {
            if (_state == LifecycleState.Sealed)
            {
                completed = _sealedComplete;
            }
            else
            {
                if (onCompleted is not null)
                {
                    _sealCompleted += onCompleted;
                }

                _sealRequested = true;
                _state = LifecycleState.Completing;
                completeNow = HasNoAdmissionsUnderLock();
            }
        }

        if (completed is { } completedValue)
        {
            InvokeSealCompleted(onCompleted, completedValue);
            return completedValue;
        }

        if (completeNow)
        {
            CompleteSeal();
        }

        lock (_lifecycleGate)
        {
            return _state == LifecycleState.Sealed && _sealedComplete;
        }
    }

    protected override object? OnSessionFinished(CoverageContextContainer context, IReadOnlyList<ModuleValue> modules)
    {
        var merged = false;
        try
        {
            var testCoverage = ProcessSessionFinished(modules, out var moduleCoverage);
            merged = _accumulator.TryMerge(moduleCoverage) != GlobalCoverageMergeResult.BecameSuppressedIncomplete;
            return testCoverage;
        }
        catch
        {
            if (!merged)
            {
                _accumulator.Suppress(GlobalCoverageFailureReason.PerTestProcessingFailed);
            }

            throw;
        }
    }

    protected override bool TryBeginSessionStartAdmission(out CoverageContextAdmission admission)
    {
        var rejected = false;
        lock (_lifecycleGate)
        {
            if (_state == LifecycleState.Sealed)
            {
                ThrowHelper.ThrowInvalidOperationException("A coverage session cannot start after the test session has ended.");
            }

            if (_state == LifecycleState.Completing)
            {
                rejected = true;
            }
            else
            {
                _inFlightStarts++;
            }
        }

        if (rejected)
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.StartFailed);
            admission = CoverageContextAdmission.Noop;
            return false;
        }

        if (!_outputManager.EnsureConfiguredAndFreeze())
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
        }

        admission = new GlobalCoverageAdmission(this);
        return true;
    }

    protected override void MarkGlobalCoverageIncomplete(GlobalCoverageFailureReason reason)
        => _accumulator.Suppress(reason);

    private void CommitAdmission(GlobalCoverageAdmission admission)
    {
        lock (_lifecycleGate)
        {
            if (admission.TryTransition(AdmissionState.Starting, AdmissionState.Active))
            {
                _inFlightStarts--;
                _activeContexts++;
            }
        }
    }

    private void FailAdmission(GlobalCoverageAdmission admission, GlobalCoverageFailureReason reason)
    {
        var completeNow = false;
        lock (_lifecycleGate)
        {
            var previous = admission.ReleaseState();
            if (previous == AdmissionState.Starting)
            {
                _inFlightStarts--;
            }
            else if (previous == AdmissionState.Active)
            {
                _activeContexts--;
            }
            else
            {
                return;
            }

            completeNow = _sealRequested && HasNoAdmissionsUnderLock();
        }

        _accumulator.Suppress(reason);
        if (completeNow)
        {
            CompleteSeal();
        }
    }

    private void ReleaseAdmission(GlobalCoverageAdmission admission)
    {
        var completeNow = false;
        lock (_lifecycleGate)
        {
            if (admission.ReleaseState() == AdmissionState.Active)
            {
                _activeContexts--;
                completeNow = _sealRequested && HasNoAdmissionsUnderLock();
            }
        }

        if (completeNow)
        {
            CompleteSeal();
        }
    }

    private void ReleaseSnapshotAdmission()
    {
        var completeNow = false;
        lock (_lifecycleGate)
        {
            if (_inFlightSnapshots > 0)
            {
                _inFlightSnapshots--;
                completeNow = _sealRequested && HasNoAdmissionsUnderLock();
            }
        }

        if (completeNow)
        {
            CompleteSeal();
        }
    }

    private bool HasNoAdmissionsUnderLock()
        => _inFlightStarts == 0 && _activeContexts == 0 && _inFlightSnapshots == 0;

    private void CompleteSeal()
    {
        lock (_lifecycleGate)
        {
            if (_state != LifecycleState.Completing || _sealStarted || !HasNoAdmissionsUnderLock())
            {
                return;
            }

            _sealStarted = true;
        }

        LogContextDiagnostics(_accumulator.AcceptedContextCount);
        var complete = TryPublishFinalSnapshot(out var failureException);
        if (!complete)
        {
            // Sealing is single-shot, so report the terminal failure here instead of logging each
            // lower-level attempt and producing duplicate diagnostics for the same coverage run.
            TelemetryFactory.Metrics.RecordCountCIVisibilityCodeCoverageErrors();
            failureException ??= _outputManager.FailureException;
            var failureReason = _accumulator.FailureReason;
            if (failureException is not null)
            {
                Log.Error<GlobalCoverageFailureReason>(failureException, "Global code coverage could not be finalized. Reason: {FailureReason}.", failureReason);
            }
            else
            {
                Log.Error<GlobalCoverageFailureReason>("Global code coverage could not be finalized. Reason: {FailureReason}.", failureReason);
            }
        }

        Action<bool>? callback;
        lock (_lifecycleGate)
        {
            _sealedComplete = complete;
            _state = LifecycleState.Sealed;
            callback = _sealCompleted;
            _sealCompleted = null;
        }

        InvokeSealCompleted(callback, complete);

        ModuleValue.LogNativeMemoryDiagnostics(DomainMetadata.Instance.ProcessId);
    }

    private bool TryPublishFinalSnapshot(out Exception? failureException)
    {
        failureException = null;
        try
        {
            var result = _accumulator.AcquireSnapshot(GlobalContainer);
            if (result.Status != GlobalCoverageSnapshotStatus.Success || result.Snapshot is not { } snapshot)
            {
                return false;
            }

            using (snapshot)
            {
                if (!_accumulator.TryFinalizeSnapshot(snapshot, () => _outputManager.TryPublish(snapshot.Model)))
                {
                    _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
                    return false;
                }

                return true;
            }
        }
        catch (Exception ex)
        {
            failureException = ex;
            _accumulator.Suppress(GlobalCoverageFailureReason.SnapshotFailed);
            return false;
        }
    }

    private void InvokeSealCompleted(Action<bool>? callback, bool complete)
    {
        try
        {
            callback?.Invoke(complete);
        }
        catch
        {
            // Publication callbacks must not replace failures from the test lifecycle.
        }
    }

    private sealed class GlobalCoverageAdmission : CoverageContextAdmission
    {
        private readonly DefaultWithGlobalCoverageEventHandler _owner;
        private int _state;

        public GlobalCoverageAdmission(DefaultWithGlobalCoverageEventHandler owner) => _owner = owner;

        public override void CommitInstalled() => _owner.CommitAdmission(this);

        public override void FailStart(GlobalCoverageFailureReason reason) => _owner.FailAdmission(this, reason);

        public override void Release() => _owner.ReleaseAdmission(this);

        public bool TryTransition(AdmissionState expected, AdmissionState next)
            => Interlocked.CompareExchange(ref _state, (int)next, (int)expected) == (int)expected;

        public AdmissionState ReleaseState()
            => (AdmissionState)Interlocked.Exchange(ref _state, (int)AdmissionState.Released);
    }

    private sealed class SnapshotAdmission
    {
        private readonly DefaultWithGlobalCoverageEventHandler _owner;
        private int _released;

        public SnapshotAdmission(DefaultWithGlobalCoverageEventHandler owner) => _owner = owner;

        public void Release()
        {
            if (Interlocked.CompareExchange(ref _released, 1, 0) == 0)
            {
                _owner.ReleaseSnapshotAdmission();
            }
        }
    }
}
