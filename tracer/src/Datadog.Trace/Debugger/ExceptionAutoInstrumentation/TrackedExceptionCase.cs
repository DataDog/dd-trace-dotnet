// <copyright file="TrackedExceptionCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;

#nullable enable
namespace Datadog.Trace.Debugger.ExceptionAutoInstrumentation
{
    /// <summary>
    /// None: Initialization has not been started (yet)
    /// Initialization and teardown process takes time and can happen concurrently
    /// Initialized: rejit for all exception related methods have been requested
    /// IsCollecting: all exception related methods have been rejited and data is being collected
    /// IsCreatingSnapshot: there is a full snapshot (not partial) that is yet to be handled by the snapshot factory ("in flight").
    /// Done: Exception information was collected and reported
    /// </summary>
    internal class TrackedExceptionCase
    {
        private volatile int _initializationOrTearDownInProgress;

        public TrackedExceptionCase(ExceptionIdentifier exceptionId, string exceptionToString)
        {
            ExceptionIdentifier = exceptionId;
            ErrorHash = MD5HashProvider.GetHash(exceptionId);
            ExceptionToString = exceptionToString;
            StartCollectingTime = DateTime.MaxValue;
        }

        public bool IsCollecting => TrackingExceptionCollectionState == ExceptionCollectionState.Collecting;

        public ExceptionIdentifier ExceptionIdentifier { get; }

        public string ErrorHash { get; }

        public string ExceptionToString { get; }

        public int NormalizedExceptionHash { get; private set; }

        public ExceptionCollectionState TrackingExceptionCollectionState { get; private set; } = ExceptionCollectionState.None;

        public bool IsDone => TrackingExceptionCollectionState == ExceptionCollectionState.Finalizing || TrackingExceptionCollectionState == ExceptionCollectionState.Done;

        public bool IsInvalidated => TrackingExceptionCollectionState == ExceptionCollectionState.Invalidated;

        public DateTime StartCollectingTime { get; private set; }

        public ExceptionCase ExceptionCase { get; private set; }

        private bool BeginInProgressState(ExceptionCollectionState exceptionCollectionState)
        {
            if (Interlocked.CompareExchange(ref _initializationOrTearDownInProgress, 1, 0) == 0)
            {
                TrackingExceptionCollectionState = exceptionCollectionState;
                return true;
            }

            return false;
        }

        private void EndInProgressState(ExceptionCollectionState exceptionCollectionState)
        {
            TrackingExceptionCollectionState = exceptionCollectionState;
            _initializationOrTearDownInProgress = 0;
        }

        private bool Initialized()
        {
            return BeginInProgressState(ExceptionCollectionState.Initializing);
        }

        public void Instrument()
        {
            // else - If there is a concurrent initialization or tearing down, ignore this case
            if (Initialized())
            {
                var @case = ExceptionCaseInstrumentationManager.Instrument(ExceptionIdentifier);
                BeginCollect(@case);
                CachedDoneExceptions.Remove(NormalizedExceptionHash);
            }
        }

        private void BeginCollect(ExceptionCase @case)
        {
            ExceptionCase = @case;
            EndInProgressState(ExceptionCollectionState.Collecting);
            StartCollectingTime = DateTime.UtcNow;
        }

        public bool Revert(int normalizedExceptionHash)
        {
            if (BeginTeardown())
            {
                NormalizedExceptionHash = normalizedExceptionHash;
                ExceptionCaseInstrumentationManager.Revert(ExceptionCase);
                EndTeardown();
                return true;
            }

            return false;
        }

        public void InvalidateCase()
        {
            EndInProgressState(ExceptionCollectionState.Invalidated);
        }

        private bool BeginTeardown()
        {
            return BeginInProgressState(ExceptionCollectionState.Finalizing);
        }

        private void EndTeardown()
        {
            EndInProgressState(ExceptionCollectionState.Done);
            ExceptionCase = default;
        }

        public override int GetHashCode()
        {
            return ExceptionIdentifier.GetHashCode();
        }

        public override bool Equals(object? obj)
        {
            if (obj is TrackedExceptionCase trackedExceptionCase)
            {
                if (ReferenceEquals(this, trackedExceptionCase))
                {
                    return true;
                }

                return trackedExceptionCase.ExceptionIdentifier.Equals(ExceptionIdentifier);
            }

            return false;
        }
    }
}
