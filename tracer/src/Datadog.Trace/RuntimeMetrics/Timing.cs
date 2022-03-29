// <copyright file="Timing.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.Trace.RuntimeMetrics
{
    internal class Timing
    {
        private double _cumulatedMilliseconds;

        public void Time(double elapsedMilliseconds)
        {
            double oldValue;

            do
            {
                oldValue = _cumulatedMilliseconds;
            }
            while (Interlocked.CompareExchange(ref _cumulatedMilliseconds, oldValue + elapsedMilliseconds, oldValue) != oldValue);
        }

        public double Clear()
        {
            return Interlocked.Exchange(ref _cumulatedMilliseconds, 0);
        }
    }
}
