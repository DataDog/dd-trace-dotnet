// <copyright file="MetricStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Threading;

#pragma warning disable SA1649 // File name should match first type name | all metrics are defined in one single file
#pragma warning disable SA1402 // File may only contain a single type    | all metrics are defined in one single file
namespace Datadog.RuntimeMetrics
{
    internal class Counter : ICounter
    {
        private double _counter;

        public double GetValue()
        {
            return _counter;
        }

        public void Inc()
        {
            double oldValue;
            double newValue;

            do
            {
                oldValue = _counter;
                newValue = oldValue + 1;
            }
            while (Interlocked.CompareExchange(ref _counter, newValue, oldValue) != oldValue);
        }
    }

    internal class IncValue : IIncValue
    {
        private double _sum;
        private double _max = double.MinValue;

        public void Add(double value)
        {
            double oldValue;
            double newValue;

            do
            {
                oldValue = _sum;
                newValue = oldValue + value;
            }
            while (Interlocked.CompareExchange(ref _sum, newValue, oldValue) != oldValue);

            do
            {
                oldValue = _max;
                if (oldValue > value)
                {
                    break;
                }

                // should be the new max
                newValue = value;
            }
            while (Interlocked.CompareExchange(ref _max, newValue, oldValue) != oldValue);
        }

        public double GetSum()
        {
            return _sum;
        }

        public double GetMax()
        {
            return _max;
        }
    }

    internal class Value : IValue
    {
        private long _max;

        public void Add(long value)
        {
            long oldValue;
            long newValue;

            do
            {
                oldValue = _max;
                if (oldValue > value)
                {
                    break;
                }

                // should be the new max
                newValue = value;
            }
            while (Interlocked.CompareExchange(ref _max, newValue, oldValue) != oldValue);
        }

        public long GetMax()
        {
            return _max;
        }
    }
}
#pragma warning restore SA1402
#pragma warning restore SA1649
