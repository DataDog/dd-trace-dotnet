// <copyright file="MassTransitExceptionStore.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit
{
    /// <summary>
    /// Stores exceptions captured from MassTransit NotifyFaulted calls.
    /// The exceptions are keyed by the Datadog span ID of the active scope at the time
    /// NotifyFaulted is called, so they can be retrieved when OnStop closes that span.
    /// </summary>
    internal static class MassTransitExceptionStore
    {
        private static readonly ConcurrentDictionary<ulong, Exception> Exceptions = new();

        /// <summary>
        /// Stores an exception for the given span ID.
        /// </summary>
        internal static void StoreException(ulong spanId, Exception exception)
        {
            if (spanId != 0 && exception != null)
            {
                Exceptions[spanId] = exception;
            }
        }

        /// <summary>
        /// Tries to retrieve and remove an exception for the given span ID.
        /// </summary>
        internal static Exception? TryGetAndRemoveException(ulong spanId)
        {
            if (spanId == 0)
            {
                return null;
            }

            if (Exceptions.TryRemove(spanId, out var exception))
            {
                return exception;
            }

            return null;
        }
    }
}
