// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Headers;

#nullable enable

namespace Datadog.Trace
{
    /// <summary>
    /// The <see cref="SpanContextExtractor"/> is responsible for extracting <see cref="ISpanContext"/> in the rare cases
    /// where the Tracer couldn't propagate it itself.
    /// </summary>
    public static class SpanContextExtractor
    {
        internal static SpanContext? Extract<TCarrier>(Tracer tracer, TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter, string? messageType = null, string? source = null)
        {
            var context = tracer.TracerManager.SpanContextPropagator.Extract(carrier, getter);
            return context.SpanContext;
        }
    }
}
