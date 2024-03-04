// <copyright file="SpanContextExtractor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.SourceGenerators;

#nullable enable

namespace Datadog.Trace
{
    /// <inheritdoc />
    public sealed class SpanContextExtractor : ISpanContextExtractor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpanContextExtractor"/> class
        /// </summary>
        [Instrumented]
        public SpanContextExtractor()
        {
        }

        /// <inheritdoc />
        [Instrumented]
        public ISpanContext? Extract<TCarrier>(TCarrier carrier, Func<TCarrier, string, IEnumerable<string?>> getter)
            => null;
    }
}
