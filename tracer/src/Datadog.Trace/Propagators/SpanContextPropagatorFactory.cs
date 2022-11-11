// <copyright file="SpanContextPropagatorFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal static class SpanContextPropagatorFactory
    {
        public static SpanContextPropagator GetSpanContextPropagator(string[] requestedInjectors, string[] requestedExtractors)
        {
            var injectors = GetPropagators<IContextInjector>(requestedInjectors);
            var extractors = GetPropagators<IContextExtractor>(requestedExtractors);

            return new SpanContextPropagator(injectors, extractors);
        }

        public static IEnumerable<TPropagator> GetPropagators<TPropagator>(string[] headerStyles)
        {
            foreach (var headerStyle in headerStyles)
            {
                if (GetPropagator(headerStyle) is TPropagator propagator)
                {
                    yield return propagator;
                }
            }
        }

        public static object? GetPropagator(string headerStyle)
        {
            switch (headerStyle)
            {
                case ContextPropagationHeaderStyle.Datadog:
                    return DatadogContextPropagator.Instance;

                case ContextPropagationHeaderStyle.W3CTraceContext:
                case ContextPropagationHeaderStyle.Deprecated.W3CTraceContext:
                    return W3CContextPropagator.Instance;

                case ContextPropagationHeaderStyle.B3MultipleHeaders:
                case ContextPropagationHeaderStyle.Deprecated.B3MultipleHeaders:
                    return B3MultipleHeaderContextPropagator.Instance;

                case ContextPropagationHeaderStyle.B3SingleHeader:
                case ContextPropagationHeaderStyle.Deprecated.B3SingleHeader:
                    return B3SingleHeaderContextPropagator.Instance;

                default:
                    return null;
            }
        }
    }
}
