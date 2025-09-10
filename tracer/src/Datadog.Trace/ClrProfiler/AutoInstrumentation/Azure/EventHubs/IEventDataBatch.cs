// <copyright file="IEventDataBatch.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventHubs
{
    /// <summary>
    /// Duck type for Azure.Messaging.EventHubs.Producer.EventDataBatch
    /// </summary>
    internal interface IEventDataBatch : IDuckType
    {
        /// <summary>
        /// Gets the number of events in the batch
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets the size of the batch in bytes
        /// </summary>
        long SizeInBytes { get; }

        /// <summary>
        /// Gets the list of diagnostic identifiers of events added to this batch
        /// </summary>
        IReadOnlyList<ITraceContextTuple> GetTraceContext();
    }
}
