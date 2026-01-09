// <copyright file="RuntimeMetricsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal sealed class RuntimeMetricsWriter : IDisposable
    {
#if NETSTANDARD
        // In < .NET Core 3.1 we don't send CommittedMemory, so we report differently on < .NET Core 3.1
        private const string ProcessMetrics = $"{MetricsNames.ThreadsCount}, {MetricsNames.CpuUserTime}, {MetricsNames.CpuSystemTime}, {MetricsNames.CpuPercentage}";
#else
        private const string ProcessMetrics = $"{MetricsNames.ThreadsCount}, {MetricsNames.CommittedMemory}, {MetricsNames.CpuUserTime}, {MetricsNames.CpuSystemTime}, {MetricsNames.CpuPercentage}";
#endif

        private const string OutOfMemoryExceptionName = "System.OutOfMemoryException";

        private static readonly Version Windows81Version = new(6, 3, 9600);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RuntimeMetricsWriter>();
        private static readonly Func<IStatsdManager, TimeSpan, bool, IRuntimeMetricsListener> InitializeListenerFunc = InitializeListener;

        [ThreadStatic]
        private static bool _inspectingFirstChanceException;

        private static int _pssConsecutiveFailures;

        private readonly Process _process;

        private readonly TimeSpan _delay;

#if NETFRAMEWORK
        private readonly Task _pushEventsTask;
#else
        private readonly Timer _timer;
#endif
        private readonly IRuntimeMetricsListener _listener;

        private readonly bool _enableProcessMetrics;
#if NETSTANDARD
        // In .NET Core <3.1 on non-Windows, Process.PrivateMemorySize64 returns 0, so we disable this.
        // https://github.com/dotnet/runtime/issues/23284
        private readonly bool _enableProcessMemory = false;
#endif

        private readonly ConcurrentDictionary<string, int> _exceptionCounts = new ConcurrentDictionary<string, int>();
        private readonly IStatsdManager _statsd;
        private int _outOfMemoryCount;

        // The time when the runtime metrics were last pushed
        private DateTime _lastUpdate;

        private TimeSpan _previousUserCpu;
        private TimeSpan _previousSystemCpu;
        private int _disposed;

        public RuntimeMetricsWriter(IStatsdManager statsd, TimeSpan delay, bool inAzureAppServiceContext)
            : this(statsd, delay, inAzureAppServiceContext, InitializeListenerFunc)
        {
        }

        internal RuntimeMetricsWriter(IStatsdManager statsd, TimeSpan delay, bool inAzureAppServiceContext, Func<IStatsdManager, TimeSpan, bool, IRuntimeMetricsListener> initializeListener)
        {
            _delay = delay;
            _statsd = statsd;
            _statsd.SetRequired(StatsdConsumer.RuntimeMetricsWriter, enabled: true);
            _lastUpdate = DateTime.UtcNow;

            try
            {
                AppDomain.CurrentDomain.FirstChanceException += FirstChanceException;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "First chance exceptions won't be monitored");
            }

            try
            {
                _process = GetCurrentProcess();

                GetCurrentProcessMetrics(out var userCpu, out var systemCpu, out _, out _);

                _previousUserCpu = userCpu;
                _previousSystemCpu = systemCpu;

                _enableProcessMetrics = true;
#if NETSTANDARD
                // In .NET Core <3.1 on non-Windows, Process.PrivateMemorySize64 returns 0, so we disable this.
                // https://github.com/dotnet/runtime/issues/23284
                _enableProcessMemory = FrameworkDescription.Instance switch
                {
                    { } x when x.IsWindows() => true, // Works on Windows
                    { } x when !x.IsCoreClr() => true, // Works on .NET Framework
                    _ when Environment.Version is { Major: >= 5 } => true, // Works on .NET 5 and above
                    _ when Environment.Version is { Major: 3, Minor: > 0 } => true, // 3.1 works
                    _ when Environment.Version is { Major: 3, Minor: 0 } => false, // 3.0 is broken on linux
                    _ => false, // everything else (i.e. <.NET Core 3.0) is broken
                };
#endif
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to get current process information");
                _enableProcessMetrics = false;
            }

            try
            {
                _listener = initializeListener(statsd, delay, inAzureAppServiceContext);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to initialize runtime listener, some runtime metrics will be missing");
            }

#if NETFRAMEWORK
            // This delay is set to infinite in tests, so don't start the loop in that case
            _pushEventsTask = delay != Timeout.InfiniteTimeSpan
                                  ? Task.Factory.StartNew(PushEventsLoop, TaskCreationOptions.LongRunning)
                                  : Task.CompletedTask;
#else
            _timer = new Timer(_ => PushEvents(), null, delay, Timeout.InfiniteTimeSpan);
#endif
        }

        /// <summary>
        /// Gets the internal exception counts, to be used for tests
        /// </summary>
        internal ConcurrentDictionary<string, int> ExceptionCounts => _exceptionCounts;

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) == 1)
            {
                Log.Debug("Disposing Runtime Metrics but it was already disposed");
                return;
            }

            Log.Debug("Disposing Runtime Metrics timer");
#if NETFRAMEWORK
            if (!_pushEventsTask.Wait(TimeSpan.FromMilliseconds(5_000)))
            {
                Log.Warning("Failed to dispose Runtime Metrics timer after 5 seconds");
            }
#else
            // Callbacks can occur after the Dispose() method overload has been called,
            // because the timer queues callbacks for execution by thread pool threads.
            // Using the Dispose(WaitHandle) method overload to waits until all callbacks have completed.
            // ManualResetEventSlim doesn't do well with Timer so need to use ManualResetEvent
            using (var manualResetEvent = new ManualResetEvent(false))
            {
                if (_timer.Dispose(manualResetEvent) && !manualResetEvent.WaitOne(5_000))
                {
                    Log.Warning("Failed to dispose Runtime Metrics timer after 5 seconds");
                }
            }
#endif
            Log.Debug("Disposing other resources for Runtime Metrics");
            AppDomain.CurrentDomain.FirstChanceException -= FirstChanceException;
            // We don't dispose runtime metrics on .NET Core because of https://github.com/dotnet/runtime/issues/103480
#if NETFRAMEWORK
            _listener?.Dispose();
#endif
            _exceptionCounts.Clear();
        }

#if NETFRAMEWORK
        internal void PushEventsLoop()
        {
            while (PushEvents())
            {
            }
        }
#endif

        internal bool PushEvents()
        {
            if (Volatile.Read(ref _disposed) == 1)
            {
                Log.Debug("Runtime metrics is disposed and can't push new events");
                return false;
            }

            var now = DateTime.UtcNow;

            try
            {
                var elapsedSinceLastUpdate = now - _lastUpdate;
                _lastUpdate = now;

                _listener?.Refresh();
                // if we can't send stats (e.g. we're shutting down), there's not much point in
                // running all this, but seeing as we update various state, play it safe and just do no-ops
                using var lease = _statsd.TryGetClientLease();
                var statsd = lease.Client ?? NoOpStatsd.Instance;

                if (_enableProcessMetrics)
                {
                    GetCurrentProcessMetrics(out var newUserCpu, out var newSystemCpu, out var threadCount, out var memoryUsage);

                    var userCpu = newUserCpu - _previousUserCpu;
                    var systemCpu = newSystemCpu - _previousSystemCpu;

                    _previousUserCpu = newUserCpu;
                    _previousSystemCpu = newSystemCpu;

                    // Note: the behavior of Environment.ProcessorCount has changed a lot accross version: https://github.com/dotnet/runtime/issues/622
                    // What we want is the number of cores attributed to the container, which is the behavior in 3.1.2+ (and, I believe, in 2.x)
                    var maximumCpu = Environment.ProcessorCount * elapsedSinceLastUpdate.TotalMilliseconds;
                    var totalCpu = userCpu + systemCpu;

                    statsd.Gauge(MetricsNames.ThreadsCount, threadCount);

#if NETSTANDARD
                    if (_enableProcessMemory)
                    {
                        statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);
                        Log.Debug("Sent the following metrics to the DD agent: {Metrics}", MetricsNames.CommittedMemory);
                    }
#else
                    statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);
#endif

                    // Get CPU time in milliseconds per second
                    statsd.Gauge(MetricsNames.CpuUserTime, userCpu.TotalMilliseconds / elapsedSinceLastUpdate.TotalSeconds);
                    statsd.Gauge(MetricsNames.CpuSystemTime, systemCpu.TotalMilliseconds / elapsedSinceLastUpdate.TotalSeconds);

                    statsd.Gauge(MetricsNames.CpuPercentage, Math.Round(totalCpu.TotalMilliseconds * 100 / maximumCpu, 1, MidpointRounding.AwayFromZero));

                    if (statsd is not NoOpStatsd)
                    {
                        Log.Debug("Sent the following metrics to the DD agent: {Metrics}", ProcessMetrics);
                    }
                }

                bool sentExceptionCount = false;

                if (Volatile.Read(ref _outOfMemoryCount) > 0)
                {
                    var oomCount = Interlocked.Exchange(ref _outOfMemoryCount, 0);
                    statsd.Increment(MetricsNames.ExceptionsCount, oomCount, tags: [$"exception_type:{OutOfMemoryExceptionName}"]);
                    sentExceptionCount = true;
                }

                if (!_exceptionCounts.IsEmpty)
                {
                    foreach (var element in _exceptionCounts)
                    {
                        statsd.Increment(MetricsNames.ExceptionsCount, element.Value, tags: [$"exception_type:{element.Key}"]);
                    }

                    // There's a race condition where we could clear items that haven't been pushed
                    // Having an exact exception count is probably not worth the overhead required to fix it
                    _exceptionCounts.Clear();
                    sentExceptionCount = true;
                }

                if (sentExceptionCount)
                {
                    Log.Debug("Sent the following metrics to the DD agent: {Metrics}", MetricsNames.ExceptionsCount);
                }
                else
                {
                    Log.Debug("Did not send the following metrics to the DD agent: {Metrics}", MetricsNames.ExceptionsCount);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while updating runtime metrics");
            }
            finally
            {
                var callbackExecutionDuration = DateTime.UtcNow - now;

#if NETFRAMEWORK
                // Ideally we'd wait for the full time, but we need to make sure we shutdown in a relatively timely fashion
                const int loopDurationMs = 200;
                var newDelay = (int)(_delay - callbackExecutionDuration).TotalMilliseconds;

                // Missed it, so just reset
                if (newDelay <= 0)
                {
                    newDelay = (int)_delay.TotalMilliseconds;
                }

                while (newDelay > 0 && Volatile.Read(ref _disposed) == 0)
                {
                    var sleepDuration = Math.Min(newDelay, loopDurationMs);
                    Thread.Sleep(sleepDuration);
                    newDelay -= sleepDuration;
                }
#else
                var newDelay = _delay - callbackExecutionDuration;

                if (newDelay < TimeSpan.Zero)
                {
                    newDelay = _delay;
                }

                try
                {
                    _timer.Change(newDelay, Timeout.InfiniteTimeSpan);
                }
                catch (ObjectDisposedException)
                {
                }
#endif
            }

            return true;
        }

        private static IRuntimeMetricsListener InitializeListener(IStatsdManager statsd, TimeSpan delay, bool inAzureAppServiceContext)
        {
#if NETCOREAPP
            return new RuntimeEventListener(statsd, delay);
#elif NETFRAMEWORK
            try
            {
                return new MemoryMappedCounters(statsd);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while initializing memory-mapped counters. Falling back to performance counters.");
                return inAzureAppServiceContext ? new AzureAppServicePerformanceCounters(statsd) : new PerformanceCountersListener(statsd);
            }
#else
            return null;
#endif
        }

        /// <summary>
        /// Wrapper around <see cref="Process.GetCurrentProcess"/>
        ///
        /// On .NET Framework the <see cref="Process"/> class is guarded by a
        /// LinkDemand for FullTrust, so partial trust callers will throw an exception.
        /// This exception is thrown when the caller method is being JIT compiled, NOT
        /// when Process.GetCurrentProcess is called, so this wrapper method allows
        /// us to catch the exception.
        /// </summary>
        /// <returns>Returns the name of the current process</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static Process GetCurrentProcess()
        {
            return Process.GetCurrentProcess();
        }

        private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            if (e == null)
            {
                // All hope is lost, stand back and watch the world burn
                return;
            }

            if (e.Exception is OutOfMemoryException)
            {
                // We have a special path for OOM because we can't allocate
                // Apparently, even reading the _inspectingFirstChanceException threadstatic field can throw
                Interlocked.Increment(ref _outOfMemoryCount);
                return;
            }

            if (_inspectingFirstChanceException)
            {
                // In rare occasions, inspecting an exception could throw another exception
                // We need to detect this to avoid infinite recursion
                return;
            }

            try
            {
                _inspectingFirstChanceException = true;

                var name = e.Exception.GetType().Name;
                _exceptionCounts.AddOrUpdate(name, 1, (_, count) => count + 1);
            }
            catch
            {
            }
            finally
            {
                _inspectingFirstChanceException = false;
            }
        }

        private void GetCurrentProcessMetrics(out TimeSpan userProcessorTime, out TimeSpan systemCpuTime, out int threadCount, out long privateMemorySize)
        {
            if (_pssConsecutiveFailures < 3 && Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version > Windows81Version)
            {
                try
                {
                    ProcessSnapshotRuntimeInformation.GetCurrentProcessMetrics(out userProcessorTime, out systemCpuTime, out threadCount, out privateMemorySize);
                    _pssConsecutiveFailures = 0;
                }
                catch
                {
                    var consecutiveFailures = Interlocked.Increment(ref _pssConsecutiveFailures);

                    if (consecutiveFailures >= 3)
                    {
                        Log.Error("Pss failed 3 times in a row, falling back to the Process API");
                    }

                    throw;
                }

                return;
            }

            _process.Refresh();
            userProcessorTime = _process.UserProcessorTime;
            systemCpuTime = _process.PrivilegedProcessorTime;
            threadCount = _process.Threads.Count;
            privateMemorySize = _process.PrivateMemorySize64;
        }
    }
}
