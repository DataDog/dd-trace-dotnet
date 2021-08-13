// <copyright file="Partition.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Kafka
{
    /// <summary>
    /// Partition for duck-typing
    /// </summary>
    [DuckCopy]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public struct Partition
    {
        /// <summary>
        /// Gets the int value corresponding to this partition
        /// </summary>
        public int Value;

        /// <summary>
        ///     Gets whether or not this is one of the special
        ///     partition values.
        /// </summary>
        public bool IsSpecial;

        /// <summary>
        /// Based on the original implementation
        /// https://github.com/confluentinc/confluent-kafka-dotnet/blob/master/src/Confluent.Kafka/Partition.cs#L217-L224
        /// </summary>
        /// <returns>A string that represents the Partition object</returns>
        public override string ToString()
        {
            return IsSpecial ? "[Any]" : $"[{Value.ToString()}]";
        }
    }
}
