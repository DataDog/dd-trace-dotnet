// <copyright file="BackgroundLoopBase.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using Datadog.Util;

namespace Datadog.Profiler
{
    /// <summary>
    /// The background loop uses a dedicated thread in order
    /// to prevent the processing done by this thread from being affected
    /// by potential thread pool starvation.
    /// It uses synchronous waits / sleeps when it is idle.
    ///
    /// It is preferable to use blocking IO (i.e. non-async-IO) directly on this
    /// thread to avoid threadpool interactions.
    /// </summary>
    internal abstract class BackgroundLoopBase : IDisposable
    {
        private const int DefaultReadyCheckPeriodSec = 10;

        private readonly string _logComponentMoniker;
        private readonly TimeSpan _readyCheckPeriod;
        private readonly string _loopThreadName;

        private int _loopState = State.NotStarted;
        private AutoResetEvent _exitEvent = null;
        private Thread _loopThread = null;

        public BackgroundLoopBase(string logComponentMoniker)
            : this(logComponentMoniker, TimeSpan.FromSeconds(DefaultReadyCheckPeriodSec))
        {
        }

        public BackgroundLoopBase(string logComponentMoniker, TimeSpan readyCheckPeriod)
        {
            Validate.NotNullOrWhitespace(logComponentMoniker, nameof(logComponentMoniker));

            if (readyCheckPeriod <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(readyCheckPeriod), $"{nameof(readyCheckPeriod)} must be positive.");
            }

            _logComponentMoniker = logComponentMoniker;
            _readyCheckPeriod = readyCheckPeriod;
            _loopThreadName = GetOrSanitizeLoopThreadName(GetLoopThreadName());
        }

        public bool Start()
        {
            int prevState = Interlocked.CompareExchange(ref _loopState, State.Running, State.NotStarted);
            if (prevState != State.NotStarted)
            {
                return false;
            }

            _exitEvent = new AutoResetEvent(false);

            _loopThread = new Thread(this.MainLoop);
            _loopThread.Name = _loopThreadName;

            // Note: if this thread is set as a foreground thread, the application would never exit because
            // the runtime stops the application after the last foreground thread ends.
            _loopThread.IsBackground = true;

            Log.Info(_logComponentMoniker, $"Starting loop thread ({_loopThread.Name} - #{_loopThread.ManagedThreadId})");

            _loopThread.Start();
            return true;
        }

        public void Shutdown()
        {
            // If we already shut down, all is good:
            int loopState = Volatile.Read(ref _loopState);
            if (loopState == State.ShutDown || loopState == State.Disposed)
            {
                return;
            }

            OnShutdownRequested();

            // If we were not started, we can transition directly to shut down:
            int prevState = Interlocked.CompareExchange(ref _loopState, State.ShutDown, State.NotStarted);
            if (prevState == State.NotStarted)
            {
                Log.Info(_logComponentMoniker, "Main loop shut down before it started", $"thread name", _loopThread.Name);

                return;
            }

            // Request shutdown:
            Interlocked.Exchange(ref _loopState, State.ShutdownRequested);

            // Signal main loop to wake up:
            _exitEvent.Set();

            // Yield thread and see if we have shut down:
            Thread.Yield();

            loopState = Volatile.Read(ref _loopState);
            if (loopState != State.ShutDown && loopState != State.Disposed)
            {
                // We have not shut down. We will now wait and periodically check until we shut down:
                int[] waitMillis = new int[] { 1, 2, 5, 10, 20, 50, 500 };
                int w = 0;

                loopState = Volatile.Read(ref _loopState);
                while (loopState != State.ShutDown && loopState != State.Disposed)
                {
                    Thread.Sleep(waitMillis[w]);

                    if (++w >= waitMillis.Length)
                    {
                        w = 0;
                    }

                    loopState = Volatile.Read(ref _loopState);
                }
            }

            Log.Info(_logComponentMoniker, "Main loop shut down", $"thread name", _loopThread.Name);

            OnShutdownCompleted();
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                int loopState = Volatile.Read(ref _loopState);
                if (loopState == State.Disposed)
                {
                    return;
                }

                // We need to signal the send loop to exit and then wait for it, before we can dispose.
                Shutdown();

                // Dispose managed state
                AutoResetEvent loopSignal = _exitEvent;
                if (loopSignal != null)
                {
                    try
                    {
                        loopSignal.Set();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(_logComponentMoniker, ex, $"thread name", _loopThread.Name);
                    }

                    try
                    {
                        loopSignal.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(_logComponentMoniker, ex, $"thread name", _loopThread.Name);
                    }

                    _exitEvent.Dispose();
                    _exitEvent = null;
                }

                _loopThread = null;

                Interlocked.Exchange(ref _loopState, State.Disposed);
            }
        }

        protected virtual string GetLoopThreadName()
        {
            return this.GetType().Name + "." + nameof(MainLoop);
        }

        protected abstract TimeSpan GetPeriod();

        protected abstract void PerformIterationWork();

        /// <summary>
        /// This is to check for some condition that occurred before the expected iteration time that should cause
        /// the iteration work to be performed earlier than originally expected (e.g. buffer full).
        /// </summary>
        protected abstract bool IsReady();

        protected virtual void OnShutdownRequested()
        {
        }

        protected virtual void OnShutdownCompleted()
        {
        }

        private void MainLoop()
        {
            int osThreadId;
            try
            {
#pragma warning disable CS0618 // GetCurrentThreadId is obsolete but we can still use it for logging purposes (see respective docs)
                // BUG: this is not an OS id: need to P/Invoke GetCurrentThreadId() on Windows
                osThreadId = AppDomain.GetCurrentThreadId();
#pragma warning restore CS0618 // Type or member is obsolete

                NativeInterop.ThreadsCpuManager_Map(osThreadId, _logComponentMoniker);
            }
            catch
            {
                osThreadId = -1;
            }

            Log.Info(
                _logComponentMoniker,
                $"Entering main loop ({_loopThread.Name} - #{_loopThread.ManagedThreadId} / {osThreadId}) at priority {_loopThread.Priority}");

            DateTimeOffset periodStart = DateTimeOffset.Now;
            while (Volatile.Read(ref _loopState) == State.Running)
            {
                try
                {
                    periodStart = DateTimeOffset.Now;
                    PerformIterationWork();
                    WaitForNextIteration(periodStart);
                }
                catch (Exception ex)
                {
                    Log.Error(_logComponentMoniker, ex, $"thread name", _loopThread.Name);
                }
            }

            Interlocked.CompareExchange(ref _loopState, State.ShutDown, State.ShutdownRequested);
        }

        // Wait for a given period except if
        //  - data is ready or
        //  - should shutdown
        private void WaitForNextIteration(DateTimeOffset periodStart)
        {
            var endOfPeriod = periodStart + GetPeriod();

            while (_loopState == State.Running)
            {
                var now = DateTimeOffset.Now;
                if (now >= endOfPeriod)
                {
                    return;
                }

                try
                {
                    // check for early readiness
                    if (IsReady())
                    {
                        return;
                    }

                    // don't wait beyond the end of the period
                    var remainingWait = endOfPeriod - now;
                    // compute the wait duration:
                    //  - if remaining is less than ready check period, just wait for remaining
                    //  - otherwise, wait for the ready check period
                    var waitDuration = _readyCheckPeriod;
                    if (_readyCheckPeriod > remainingWait)
                    {
                        waitDuration = remainingWait;
                    }

                    // check for shutdown if any
                    if (_exitEvent.WaitOne(waitDuration))
                    {
                        // _loopState should = State.ShutdownRequested
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(_logComponentMoniker, ex, $"thread name", _loopThread.Name);
                }
            }
        }

        private string GetOrSanitizeLoopThreadName(string loopThreadName)
        {
            if (string.IsNullOrWhiteSpace(loopThreadName))
            {
                return this.GetType().Name + "." + nameof(MainLoop);
            }
            else
            {
                return loopThreadName.Trim();
            }
        }

        private static class State
        {
            public const int NotStarted = 1;
            public const int Running = 2;
            public const int ShutdownRequested = 3;
            public const int ShutDown = 4;
            public const int Disposed = 4;
        }
    }
}