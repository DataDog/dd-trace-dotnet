// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace
{
    /// <inheritdoc />
    public class SpanContextExtractor : ISpanContextExtractor
    {
        /// <inheritdoc />
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
            => SpanContextPropagator.Instance.Extract(carrier, getter);
    }
}
