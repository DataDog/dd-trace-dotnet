// <copyright file="IKeepRateCalculator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.Agent
{
    internal interface IKeepRateCalculator
    {
        /// <summary>
        /// Increment the number of kept traces
        /// </summary>
        void IncrementKeeps(int count);

        /// <summary>
        /// Increment the number of dropped traces
        /// </summary>
        void IncrementDrops(int count);

        /// <summary>
        /// Get the current keep rate for traces
        /// </summary>
        double GetKeepRate();

        /// <summary>
        /// Stop updating the buckets. The current Keep rate can continue to be read.
        /// </summary>
        void CancelUpdates();
    }
}
