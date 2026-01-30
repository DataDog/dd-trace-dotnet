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
    /// The exceptions are keyed by Activity.TraceId so they can be retrieved
    /// when the DiagnosticObserver receives the Stop event. We use TraceId instead
    /// of Activity.Id because NotifyFaulted may be called from a child activity
    /// (e.g., Handle or Saga) while the DiagnosticObserver Stop event fires on
    /// the parent activity (Consume).
    /// </summary>
    internal static class MassTransitExceptionStore
    {
        private static readonly ConcurrentDictionary<string, Exception> Exceptions = new();

        /// <summary>
        /// Stores an exception for the given trace ID.
        /// </summary>
        internal static void StoreException(string traceId, Exception exception)
        {
            if (!string.IsNullOrEmpty(traceId) && exception != null)
            {
                Exceptions[traceId] = exception;
            }
        }

        /// <summary>
        /// Tries to retrieve and remove an exception for the given trace ID.
        /// </summary>
        internal static Exception? TryGetAndRemoveException(string traceId)
        {
            if (string.IsNullOrEmpty(traceId))
            {
                return null;
            }

            if (Exceptions.TryRemove(traceId, out var exception))
            {
                return exception;
            }

            return null;
        }
    }
}
