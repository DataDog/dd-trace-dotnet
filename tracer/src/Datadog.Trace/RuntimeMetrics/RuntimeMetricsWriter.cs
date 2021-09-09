// <copyright file="RuntimeMetricsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class RuntimeMetricsWriter : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<RuntimeMetricsWriter>();

        private readonly TimeSpan _delay;

        private readonly IDogStatsd _statsd;
        private readonly Timer _timer;

        private readonly IRuntimeMetricsListener _listener;

        private readonly bool _enableProcessMetrics;

        private readonly ConcurrentDictionary<string, int> _exceptionCounts = new ConcurrentDictionary<string, int>();

        private TimeSpan _previousUserCpu;
        private TimeSpan _previousSystemCpu;

        public RuntimeMetricsWriter(IDogStatsd statsd, TimeSpan delay)
            : this(statsd, delay, InitializeListener)
        {
        }

        internal RuntimeMetricsWriter(IDogStatsd statsd, TimeSpan delay, Func<IDogStatsd, TimeSpan, IRuntimeMetricsListener> initializeListener)
        {
            _delay = delay;
            _statsd = statsd;
            _timer = new Timer(_ => PushEvents(), null, delay, delay);

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
                ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var userCpu, out var systemCpu, out _, out _);

                _previousUserCpu = userCpu;
                _previousSystemCpu = systemCpu;

                _enableProcessMetrics = true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to get current process information");
                _enableProcessMetrics = false;
            }

            try
            {
                _listener = initializeListener(statsd, delay);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to initialize runtime listener, some runtime metrics will be missing");
            }
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
            try
            {
                _listener?.Refresh();

                if (_enableProcessMetrics)
                {
                    ProcessHelpers.GetCurrentProcessRuntimeMetrics(out var newUserCpu, out var newSystemCpu, out var threadCount, out var memoryUsage);

                    var userCpu = newUserCpu - _previousUserCpu;
                    var systemCpu = newSystemCpu - _previousSystemCpu;

                    _previousUserCpu = newUserCpu;
                    _previousSystemCpu = newSystemCpu;

                    // Note: the behavior of Environment.ProcessorCount has changed a lot accross version: https://github.com/dotnet/runtime/issues/622
                    // What we want is the number of cores attributed to the container, which is the behavior in 3.1.2+ (and, I believe, in 2.x)
                    var maximumCpu = Environment.ProcessorCount * _delay.TotalMilliseconds;
                    var totalCpu = userCpu + systemCpu;

                    _statsd.Gauge(MetricsNames.ThreadsCount, threadCount);

                    _statsd.Gauge(MetricsNames.CommittedMemory, memoryUsage);

                    // Get CPU time in milliseconds per second
                    _statsd.Gauge(MetricsNames.CpuUserTime, userCpu.TotalMilliseconds / _delay.TotalSeconds);
                    _statsd.Gauge(MetricsNames.CpuSystemTime, systemCpu.TotalMilliseconds / _delay.TotalSeconds);

                    _statsd.Gauge(MetricsNames.CpuPercentage, Math.Round(totalCpu.TotalMilliseconds * 100 / maximumCpu, 1, MidpointRounding.AwayFromZero));
                }

                if (!_exceptionCounts.IsEmpty)
                {
                    foreach (var element in _exceptionCounts)
                    {
                        _statsd.Increment(MetricsNames.ExceptionsCount, element.Value, tags: new[] { $"exception_type:{element.Key}" });
                    }

                    // There's a race condition where we could clear items that haven't been pushed
                    // Having an exact exception count is probably not worth the overhead required to fix it
                    _exceptionCounts.Clear();
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while updating runtime metrics");
            }
        }

        private static IRuntimeMetricsListener InitializeListener(IDogStatsd statsd, TimeSpan delay)
        {
#if NETCOREAPP
            return new RuntimeEventListener(statsd, delay);
#elif NETFRAMEWORK
            return AzureAppServices.Metadata.IsRelevant ? new AzureAppServicePerformanceCounters(statsd) : new PerformanceCountersListener(statsd);
#else
            return null;
#endif
        }

        private void FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            var name = e.Exception.GetType().Name;

            _exceptionCounts.AddOrUpdate(name, 1, (_, count) => count + 1);
        }
    }
}
