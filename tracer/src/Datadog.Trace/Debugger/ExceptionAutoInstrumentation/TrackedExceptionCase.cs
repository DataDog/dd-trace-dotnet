// <copyright file="TrackedExceptionCase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Trace.Debugger.RateLimiting;

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

        public TrackedExceptionCase(ExceptionIdentifier exceptionId, TimeSpan windowDuration)
        {
            ExceptionIdentifier = exceptionId;
            ErrorHash = MD5HashProvider.GetHash(exceptionId);
            StartCollectingTime = DateTime.MaxValue;
            Sampler = new AdaptiveSampler(windowDuration, 1, 180, 16, null);
        }

        public bool IsCollecting => TrackingExceptionCollectionState == ExceptionCollectionState.Collecting;

        public ExceptionIdentifier ExceptionIdentifier { get; }

        public string ErrorHash { get; }

        public ExceptionCollectionState TrackingExceptionCollectionState { get; private set; } = ExceptionCollectionState.None;

        public bool IsDone => TrackingExceptionCollectionState == ExceptionCollectionState.Finalizing || TrackingExceptionCollectionState == ExceptionCollectionState.Done;

        public DateTime StartCollectingTime { get; private set; }

        public ExceptionCase ExceptionCase { get; set; }

        public AdaptiveSampler Sampler { get; }

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

        public bool Initialized()
        {
            return BeginInProgressState(ExceptionCollectionState.Initializing);
        }

        public void BeginCollect()
        {
            EndInProgressState(ExceptionCollectionState.Collecting);
            StartCollectingTime = DateTime.UtcNow;
        }

        public bool BeginTeardown()
        {
            return BeginInProgressState(ExceptionCollectionState.Finalizing);
        }

        public void EndTeardown()
        {
            EndInProgressState(ExceptionCollectionState.Done);
        }

        public override int GetHashCode()
        {
            return ExceptionIdentifier.GetHashCode();
        }

        public override bool Equals(object obj)
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
