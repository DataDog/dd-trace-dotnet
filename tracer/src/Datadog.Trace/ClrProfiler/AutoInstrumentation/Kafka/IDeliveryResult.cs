// <copyright file="IDeliveryResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// DeliveryResult interface for duck-typing
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IDeliveryResult
    {
        /// <summary>
        ///     Gets the Kafka partition.
        /// </summary>
        public Partition Partition { get; }

        /// <summary>
        ///     Gets the Kafka offset
        /// </summary>
        public Offset Offset { get; }
    }
}
