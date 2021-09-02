// <copyright file="IProduceException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// ProduceException interface for duck-typing
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public interface IProduceException
    {
        /// <summary>
        /// Gets the delivery result associated with the produce request
        /// </summary>
        public IDeliveryResult DeliveryResult { get; }
    }
}
