// <copyright file="SpanContextPropagatorFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace Datadog.Trace.Propagators
{
    internal static class SpanContextPropagatorFactory
    {
        public static SpanContextPropagator GetSpanContextPropagator(string[] requestedInjectors, string[] requestedExtractors, bool propagationExtractFirst)
        {
            var injectors = GetPropagators<IContextInjector>(requestedInjectors);
            var extractors = GetPropagators<IContextExtractor>(requestedExtractors);

            return new SpanContextPropagator(injectors, extractors, propagationExtractFirst);
        }

        public static IEnumerable<TPropagator> GetPropagators<TPropagator>(string[] headerStyles)
        {
            if (headerStyles is null or { Length: 0 })
            {
                yield break;
            }

            foreach (var headerStyle in headerStyles.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (GetPropagator(headerStyle) is TPropagator propagator)
                {
                    yield return propagator;
                }
            }
        }

        public static object? GetPropagator(string headerStyle)
        {
            if (string.Equals(headerStyle, ContextPropagationHeaderStyle.Datadog, StringComparison.OrdinalIgnoreCase))
            {
                return DatadogContextPropagator.Instance;
            }

            if (string.Equals(headerStyle, ContextPropagationHeaderStyle.W3CTraceContext, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerStyle, ContextPropagationHeaderStyle.Deprecated.W3CTraceContext, StringComparison.OrdinalIgnoreCase))
            {
                return W3CTraceContextPropagator.Instance;
            }

            if (string.Equals(headerStyle, ContextPropagationHeaderStyle.B3MultipleHeaders, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerStyle, ContextPropagationHeaderStyle.Deprecated.B3MultipleHeaders, StringComparison.OrdinalIgnoreCase))
            {
                return B3MultipleHeaderContextPropagator.Instance;
            }

            if (string.Equals(headerStyle, ContextPropagationHeaderStyle.B3SingleHeader, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(headerStyle, ContextPropagationHeaderStyle.Deprecated.B3SingleHeader, StringComparison.OrdinalIgnoreCase))
            {
                return B3SingleHeaderContextPropagator.Instance;
            }

            return null;
        }
    }
}
