// <copyright file="PerformanceCounterWrapper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Diagnostics;
using System.Threading;
using Datadog.Trace.Logging;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class PerformanceCounterWrapper : IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<PerformanceCounterWrapper>();

        private readonly string _categoryName;
        private readonly string _counterName;

        private PerformanceCounter _counter;

        public PerformanceCounterWrapper(string categoryName, string counterName, string instanceName)
        {
            _categoryName = categoryName;
            _counterName = counterName;

            _counter = new PerformanceCounter(categoryName, counterName, instanceName, readOnly: true);
        }

        public void Dispose()
        {
            _counter?.Dispose();
        }

        public double? GetValue(string instanceName)
        {
            var counter = _counter;

            if (counter != null)
            {
                try
                {
                    counter.InstanceName = instanceName;
                    return counter.NextSample().RawValue;
                }
                catch (InvalidOperationException)
                {
                    RefreshCounter(instanceName);
                }
            }

            return null;
        }

        private void RefreshCounter(string instanceName)
        {
            try
            {
                var newCounter = new PerformanceCounter(_categoryName, _counterName, instanceName, readOnly: true);

                var oldCounter = Interlocked.Exchange(ref _counter, newCounter);

                oldCounter?.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while renewing counter");
            }
        }
    }
}
#endif
