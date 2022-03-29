// <copyright file="Timing.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Threading;

namespace Datadog.RuntimeMetrics
{
    internal class Timing
    {
        private double _cumulatedMilliseconds;

        public void Time(double elapsedMilliseconds)
        {
            double oldValue;
            double newValue;
            do
            {
                oldValue = _cumulatedMilliseconds;
                newValue = oldValue + elapsedMilliseconds;
            }
            while (Interlocked.CompareExchange(ref _cumulatedMilliseconds, newValue, oldValue) != oldValue);
        }

        public double GetTime()
        {
            return _cumulatedMilliseconds;
        }

        public double Clear()
        {
            return Interlocked.Exchange(ref _cumulatedMilliseconds, 0);
        }
    }
}
