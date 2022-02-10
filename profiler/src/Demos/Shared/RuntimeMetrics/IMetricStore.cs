// <copyright file="IMetricStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

#pragma warning disable SA1649 // File name should match first type name | all metrics are defined in one single file
namespace Datadog.RuntimeMetrics
{
    // Keeps track of maximum of values
    // ex: LOH size over time
    internal interface IValue
    {
        void Add(long value);

        long GetMax();
    }

    // Keeps track of maximum and sum of values
    // ex: GC suspension time
    internal interface IIncValue
    {
        void Add(double value);

        double GetSum();

        double GetMax();
    }

    // Keeps track of a count one by one
    // ex: number of GCs
    internal interface ICounter
    {
        void Inc();

        double GetValue();
    }
}
#pragma warning restore SA1649