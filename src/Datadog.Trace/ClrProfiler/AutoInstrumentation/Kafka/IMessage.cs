// <copyright file="IMessage.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Message interface for duck-typing
    /// </summary>
    public interface IMessage
    {
        /// <summary>
        /// Gets the value of the message
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Gets the timestamp that the message was produced
        /// </summary>
        public ITimestamp Timestamp { get; }

        /// <summary>
        /// Gets or sets the headers for the record
        /// </summary>
        public IHeaders Headers { get; set; }
    }
}
