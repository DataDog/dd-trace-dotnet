// <copyright file="DefaultWithGlobalCoverageEventHandler.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class DefaultWithGlobalCoverageEventHandler : DefaultCoverageEventHandler
{
    private readonly object _lifecycleGate = new();
    private readonly GlobalCoverageAccumulator _accumulator;
    private readonly GlobalCoverageOutputManager _outputManager;
    private int _inFlightStarts;
    private int _activeContexts;
    private int _inFlightFinalizers;
    private LifecycleState _lifecycleState;
    private Action<bool>? _sealCompleted;
    private bool _sealCompletionStarted;
    private bool _sealRequested;
    private bool _publishFinalSnapshotOnSeal;
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

    public enum LifecycleState
    {
        Running,
        Completing,
        Sealed,
    }

    public GlobalCoverageAccumulatorSnapshot AccumulatorDiagnostics => _accumulator.GetDiagnostics();

    public int ActiveContexts
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _activeContexts;
            }
        }
    }

    public LifecycleState State
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _lifecycleState;
            }
        }
    }

    public int InFlightStarts
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _inFlightStarts;
            }
        }
    }

    public int InFlightFinalizers
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _inFlightFinalizers;
            }
        }
    }

    public bool SealedComplete
    {
        get
        {
            lock (_lifecycleGate)
            {
                return _lifecycleState == LifecycleState.Sealed && _sealedComplete;
            }
        }
    }

    public IReadOnlyList<GlobalCoverageOutputRegistration> OutputRegistrations => _outputManager.GetRegistrations();

    public void MarkIncomplete(GlobalCoverageFailureReason reason)
        => _accumulator.Suppress(reason);

    public GlobalCoverageSnapshotResult AcquireGlobalCoverageSnapshot()
    {
        if (!_outputManager.EnsureConfiguredAndFreeze())
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
        }

        var finalizerAdmission = new FinalizerAdmission(this);
        var admitted = false;
        var suppress = false;
        lock (_lifecycleGate)
        {
            if (_lifecycleState == LifecycleState.Running)
            {
                _inFlightFinalizers++;
                admitted = true;
            }
            else
            {
                suppress = _lifecycleState == LifecycleState.Completing;
            }
        }

        if (!admitted)
        {
            if (suppress)
            {
                _accumulator.Suppress(GlobalCoverageFailureReason.SnapshotFailed);
            }

            return GlobalCoverageSnapshotResult.Suppressed(_accumulator.FailureReason);
        }

        try
        {
            var result = _accumulator.AcquireSnapshot(GlobalContainer, finalizerAdmission.Release);
            if (result.Status == GlobalCoverageSnapshotStatus.Success && result.Snapshot is { } snapshot)
            {
                try
                {
                    InitializeSnapshotOutput(snapshot);
                }
                catch
                {
                    snapshot.Dispose();
                    throw;
                }
            }
            else
            {
                finalizerAdmission.Release();
            }

            return result;
        }
        catch
        {
            finalizerAdmission.Release();
            throw;
        }
    }

    public bool TryCommit(GlobalCoverageSnapshot snapshot, Action action)
        => _accumulator.TryCommit(snapshot, action);

    public bool TryPublishRequiredFiles(GlobalCoverageSnapshot snapshot)
    {
        var stagedArtifacts = new List<StagedOutput>();
        try
        {
            var writer = new GlobalCoverageArtifactWriter();
            foreach (var registration in _outputManager.GetRegistrations())
            {
                if ((snapshot.RequiredOutputMask & registration.Bit) == 0)
                {
                    continue;
                }

                var outputPath = _outputManager.GetCoveragePath(registration, snapshot.GenerationId);
                stagedArtifacts.Add(new StagedOutput(registration.Bit, writer.StageNoReplace(outputPath, snapshot.Model)));
            }

            return TryCommit(
                       snapshot,
                       () =>
                       {
                           foreach (var staged in stagedArtifacts)
                           {
                               staged.Artifact.Commit();
                               snapshot.RecordOutputCommit(staged.Bit);
                           }
                       }) &&
                   snapshot.RequiredOutputMask == snapshot.CommittedOutputMask;
        }
        catch
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
            throw;
        }
        finally
        {
            foreach (var staged in stagedArtifacts)
            {
                staged.Artifact.Dispose();
            }
        }
    }

    public bool RegisterCollectorOutputDirectory(string directory)
    {
        var registered = _outputManager.RegisterCollectorAndFreeze(directory);
        if (!registered)
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
        }

        return registered;
    }

    public bool FinalizeAndSeal(Action<bool>? onCompleted = null) => RequestSeal(publishFinalSnapshot: true, onCompleted);

    public bool RequestSeal() => RequestSeal(publishFinalSnapshot: false, onCompleted: null);

    private bool RequestSeal(bool publishFinalSnapshot, Action<bool>? onCompleted)
    {
        var audit = false;
        bool? completed = null;
        lock (_lifecycleGate)
        {
            if (_lifecycleState == LifecycleState.Sealed)
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
                _publishFinalSnapshotOnSeal |= publishFinalSnapshot;
                _lifecycleState = LifecycleState.Completing;
                audit = HasNoAdmissionsUnderLock();
            }
        }

        if (completed is { } completedValue)
        {
            InvokeSealCompleted(onCompleted, completedValue);
            return completedValue;
        }

        if (audit)
        {
            CompleteSeal();
        }

        lock (_lifecycleGate)
        {
            return _lifecycleState == LifecycleState.Sealed && _sealedComplete;
        }
    }

    private void InitializeSnapshotOutput(GlobalCoverageSnapshot snapshot)
        => snapshot.InitializeOutput(_outputManager.FrozenMask, OnSnapshotDisposed);

    protected override object? OnSessionFinished(CoverageContextContainer context, IReadOnlyList<ModuleValue> modules)
    {
        var merged = false;
        try
        {
            var testCoverage = ProcessSessionFinished(modules, out var moduleCoverage);
            var mergeResult = _accumulator.TryMerge(moduleCoverage);
            merged = mergeResult != GlobalCoverageMergeResult.BecameSuppressedIncomplete;
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
            if (_lifecycleState == LifecycleState.Sealed)
            {
                ThrowHelper.ThrowInvalidOperationException("A coverage session cannot start after the test session has ended.");
            }

            if (_lifecycleState == LifecycleState.Completing)
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
            if (!admission.TryTransition(AdmissionState.Starting, AdmissionState.Active))
            {
                return;
            }

            _inFlightStarts--;
            _activeContexts++;
        }
    }

    private void FailAdmission(GlobalCoverageAdmission admission, GlobalCoverageFailureReason reason)
    {
        var audit = false;
        lock (_lifecycleGate)
        {
            var priorState = admission.ReleaseState();
            if (priorState == AdmissionState.Starting)
            {
                _inFlightStarts--;
            }
            else if (priorState == AdmissionState.Active)
            {
                _activeContexts--;
            }
            else
            {
                return;
            }

            audit = _sealRequested && HasNoAdmissionsUnderLock();
        }

        _accumulator.Suppress(reason);
        if (audit)
        {
            CompleteSeal();
        }
    }

    private void ReleaseAdmission(GlobalCoverageAdmission admission)
    {
        var audit = false;
        lock (_lifecycleGate)
        {
            if (admission.ReleaseState() == AdmissionState.Active)
            {
                _activeContexts--;
                audit = _sealRequested && HasNoAdmissionsUnderLock();
            }
        }

        if (audit)
        {
            CompleteSeal();
        }
    }

    private void ReleaseFinalizerAdmission()
    {
        var audit = false;
        lock (_lifecycleGate)
        {
            if (_inFlightFinalizers > 0)
            {
                _inFlightFinalizers--;
                audit = _sealRequested && HasNoAdmissionsUnderLock();
            }
        }

        if (audit)
        {
            CompleteSeal();
        }
    }

    private bool HasNoAdmissionsUnderLock()
        => _inFlightStarts == 0 && _activeContexts == 0 && _inFlightFinalizers == 0;

    private void CompleteSeal()
    {
        var diagnostics = ContextDiagnostics;
        var balanced = diagnostics.Started == diagnostics.Closed &&
                       diagnostics.Closed == diagnostics.Disposed;
        bool publishFinalSnapshot;
        lock (_lifecycleGate)
        {
            if (_lifecycleState != LifecycleState.Completing ||
                _sealCompletionStarted ||
                !HasNoAdmissionsUnderLock())
            {
                return;
            }

            _sealCompletionStarted = true;
            publishFinalSnapshot = _publishFinalSnapshotOnSeal;
        }

        // Admissions must be closed before capturing the terminal generation. Otherwise, a context
        // that closes during finalization can merge into the replacement generation after the
        // published snapshot and be omitted from the ready protocol set.
        var finalSnapshotPublished = !publishFinalSnapshot || TryPublishFinalSnapshot();
        using var stagedReadyMarkers = balanced && finalSnapshotPublished ? _outputManager.TryStageReadyMarkers(diagnostics) : null;
        var complete = stagedReadyMarkers is not null &&
                       _accumulator.TryFinalizeCompleteness(stagedReadyMarkers.Commit);
        Action<bool>? sealCompleted;
        lock (_lifecycleGate)
        {
            _sealedComplete = complete;
            _lifecycleState = LifecycleState.Sealed;
            sealCompleted = _sealCompleted;
            _sealCompleted = null;
        }

        InvokeSealCompleted(sealCompleted, complete);

        var accumulatorDiagnostics = _accumulator.GetDiagnostics();
        var processId = DomainMetadata.Instance.ProcessId;
        TestOptimization.Instance.Log.Debug<int, long, long, long, long>(
            "Global coverage context diagnostics: pid={ProcessId}, started={Started}, closed={Closed}, disposed={Disposed}, merged={Merged}.",
            processId,
            diagnostics.Started,
            diagnostics.Closed,
            diagnostics.Disposed,
            accumulatorDiagnostics.AcceptedContextCount);
        ModuleValue.LogNativeMemoryDiagnostics(processId);
    }

    private bool TryPublishFinalSnapshot()
    {
        GlobalCoverageSnapshot? snapshot = null;
        try
        {
            if (!_outputManager.EnsureConfiguredAndFreeze())
            {
                _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
                return false;
            }

            // Per-test buffers have already been compacted and released. The process-wide fallback
            // is bounded by the instrumented modules, so capture it without changing probe lifetime.
            var result = _accumulator.AcquireSnapshot(GlobalContainer);
            if (result.Status != GlobalCoverageSnapshotStatus.Success || result.Snapshot is not { } acquiredSnapshot)
            {
                return false;
            }

            snapshot = acquiredSnapshot;
            InitializeSnapshotOutput(snapshot);
            return TryPublishRequiredFiles(snapshot);
        }
        catch
        {
            // CompleteSeal can run while a coverage context is unwinding. Keep cleanup non-throwing
            // while leaving the process pending so reconciliation fails closed.
            _accumulator.Suppress(GlobalCoverageFailureReason.SnapshotFailed);
            return false;
        }
        finally
        {
            snapshot?.Dispose();
        }
    }

    private void OnSnapshotDisposed(GlobalCoverageSnapshot snapshot)
    {
        _outputManager.RecordGenerationCommit(snapshot.RequiredOutputMask, snapshot.CommittedOutputMask);
        if (snapshot.RequiredOutputMask != snapshot.CommittedOutputMask)
        {
            _accumulator.Suppress(GlobalCoverageFailureReason.OutputCommitFailed);
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

        public GlobalCoverageAdmission(DefaultWithGlobalCoverageEventHandler owner)
        {
            _owner = owner;
        }

        public override void CommitInstalled() => _owner.CommitAdmission(this);

        public override void FailStart(GlobalCoverageFailureReason reason) => _owner.FailAdmission(this, reason);

        public override void Release() => _owner.ReleaseAdmission(this);

        public bool TryTransition(AdmissionState expected, AdmissionState next)
            => Interlocked.CompareExchange(ref _state, (int)next, (int)expected) == (int)expected;

        public AdmissionState ReleaseState()
            => (AdmissionState)Interlocked.Exchange(ref _state, (int)AdmissionState.Released);
    }

    private sealed class FinalizerAdmission
    {
        private readonly DefaultWithGlobalCoverageEventHandler _owner;
        private int _released;

        public FinalizerAdmission(DefaultWithGlobalCoverageEventHandler owner)
        {
            _owner = owner;
        }

        public void Release()
        {
            if (Interlocked.CompareExchange(ref _released, 1, 0) == 0)
            {
                _owner.ReleaseFinalizerAdmission();
            }
        }
    }

    private sealed class StagedOutput
    {
        public StagedOutput(byte bit, GlobalCoverageStagedArtifact artifact)
        {
            Bit = bit;
            Artifact = artifact;
        }

        public byte Bit { get; }

        public GlobalCoverageStagedArtifact Artifact { get; }
    }
}
