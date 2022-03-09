// <copyright file="ContextPropagators.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.Propagators
{
    internal static class ContextPropagators
    {
        private static readonly IReadOnlyDictionary<string, Type> AvailablePropagators = new Dictionary<string, Type>
        {
            { nameof(Names.Datadog), typeof(DatadogContextPropagator) },
            { nameof(Names.W3C), typeof(W3CContextPropagator) },
            { nameof(Names.B3), typeof(B3ContextPropagator) },
            { nameof(Names.B3SingleHeader), typeof(B3SingleHeaderContextPropagator) },
            { "B3 single header", typeof(B3SingleHeaderContextPropagator) },
        };

        public enum Names
        {
            Datadog,
            W3C,
            B3,
            B3SingleHeader,
        }

        public static SpanContextPropagator GetSpanContextPropagator(IEnumerable<string> requestedInjectors, IEnumerable<string> requestedExtractors)
        {
            var propagatorInstances = new Dictionary<string, object>(4, StringComparer.InvariantCultureIgnoreCase);
            var selectedInjectors = new List<IContextInjector>(4);
            var selectedExtractors = new List<IContextExtractor>(4)
            {
                new DistributedContextExtractor(),
            };

            foreach (var injector in requestedInjectors)
            {
                if (AvailablePropagators.TryGetValue(injector, out var injectorType))
                {
                    if (!propagatorInstances.TryGetValue(injector, out var existingInstance))
                    {
                        var instance = (IContextInjector)Activator.CreateInstance(injectorType)!;
                        selectedInjectors.Add(instance);
                        propagatorInstances[injector] = instance;
                    }
                    else
                    {
                        selectedInjectors.Add((IContextInjector)existingInstance);
                    }
                }
            }

            foreach (var extractor in requestedExtractors)
            {
                if (AvailablePropagators.TryGetValue(extractor, out var extractorType))
                {
                    if (!propagatorInstances.TryGetValue(extractor, out var existingInstance))
                    {
                        var instance = (IContextExtractor)Activator.CreateInstance(extractorType)!;
                        selectedExtractors.Add(instance);
                        propagatorInstances[extractor] = instance;
                    }
                    else
                    {
                        selectedExtractors.Add((IContextExtractor)existingInstance);
                    }
                }
            }

            return new SpanContextPropagator(selectedInjectors, selectedExtractors);
        }
    }
}
