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
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class RuntimeMetricsWriter : IDisposable
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
        private static readonly Func<IDogStatsd, TimeSpan, bool, IRuntimeMetricsListener> InitializeListenerFunc = InitializeListener;

        [ThreadStatic]
        private static bool _inspectingFirstChanceException;

        private static int _pssConsecutiveFailures;

        private readonly Process _process;

        private readonly TimeSpan _delay;

        private readonly IDogStatsd _statsd;
        private readonly Timer _timer;

        private readonly IRuntimeMetricsListener _listener;

        private readonly bool _enableProcessMetrics;
#if NETSTANDARD
        // In .NET Core <3.1 on non-Windows, Process.PrivateMemorySize64 returns 0, so we disable this.
        // https://github.com/dotnet/runtime/issues/23284
        private readonly bool _enableProcessMemory = false;
#endif

        private readonly ConcurrentDictionary<string, int> _exceptionCounts = new ConcurrentDictionary<string, int>();
        private int _outOfMemoryCount;

        // The time when the runtime metrics were last pushed
        private DateTime _lastUpdate;

        private TimeSpan _previousUserCpu;
        private TimeSpan _previousSystemCpu;

        public RuntimeMetricsWriter(IDogStatsd statsd, TimeSpan delay, bool inAzureAppServiceContext)
            : this(statsd, delay, inAzureAppServiceContext, InitializeListenerFunc)
        {
        }

        internal RuntimeMetricsWriter(IDogStatsd statsd, TimeSpan delay, bool inAzureAppServiceContext, Func<IDogStatsd, TimeSpan, bool, IRuntimeMetricsListener> initializeListener)
        {
            _delay = delay;
            _statsd = statsd;
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

            _timer = new Timer(_ => PushEvents(), null, delay, Timeout.InfiniteTimeSpan);
        }

        /// <summary>
        /// Gets the internal exception counts, to be used for tests
        /// </summary>
        internal ConcurrentDictionary<string, int> ExceptionCounts => _exceptionCounts;

        public void Dispose()
        {
            AppDomain.CurrentDomain.FirstChanceException -= FirstChanceException;
            _timer.Dispose();
            _listener?.Dispose();
            _exceptionCounts.Clear();
        }

        internal void PushEvents()
        {
            var now = DateTime.UtcNow;

            try
            {
                var elapsedSinceLastUpdate = now - _lastUpdate;
                _lastUpdate = now;

                _listener?.Refresh();

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

                    _statsd.Gauge(MetricsNames.ThreadsCount, threadCount);

#if NETSTANDARD
                    if (_enableProcessMemory)
                    {
                        _statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);
                        Log.Debug("Sent the following metrics to the DD agent: {Metrics}", MetricsNames.CommittedMemory);
                    }
#else
                    _statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);
#endif

                    // Get CPU time in milliseconds per second
                    _statsd.Gauge(MetricsNames.CpuUserTime, userCpu.TotalMilliseconds / elapsedSinceLastUpdate.TotalSeconds);
                    _statsd.Gauge(MetricsNames.CpuSystemTime, systemCpu.TotalMilliseconds / elapsedSinceLastUpdate.TotalSeconds);

                    _statsd.Gauge(MetricsNames.CpuPercentage, Math.Round(totalCpu.TotalMilliseconds * 100 / maximumCpu, 1, MidpointRounding.AwayFromZero));

                    Log.Debug("Sent the following metrics to the DD agent: {Metrics}", ProcessMetrics);
                }

                bool sentExceptionCount = false;

                if (Volatile.Read(ref _outOfMemoryCount) > 0)
                {
                    var oomCount = Interlocked.Exchange(ref _outOfMemoryCount, 0);
                    _statsd.Increment(MetricsNames.ExceptionsCount, oomCount, tags: [$"exception_type:{OutOfMemoryExceptionName}"]);
                    sentExceptionCount = true;
                }

                if (!_exceptionCounts.IsEmpty)
                {
                    foreach (var element in _exceptionCounts)
                    {
                        _statsd.Increment(MetricsNames.ExceptionsCount, element.Value, tags: [$"exception_type:{element.Key}"]);
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
            }
        }

        private static IRuntimeMetricsListener InitializeListener(IDogStatsd statsd, TimeSpan delay, bool inAzureAppServiceContext)
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
