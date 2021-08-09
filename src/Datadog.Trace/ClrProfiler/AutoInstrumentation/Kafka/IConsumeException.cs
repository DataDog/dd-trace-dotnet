// <copyright file="IConsumeException.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// ConsumeException interface for duck-typing
    /// </summary>
    public interface IConsumeException
    {
        /// <summary>
        /// Gets the consume result associated with the consume request
        /// </summary>
        public IConsumeResult ConsumerRecord { get; }
    }
}
