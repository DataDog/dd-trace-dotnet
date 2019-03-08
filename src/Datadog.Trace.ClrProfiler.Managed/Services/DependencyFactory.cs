using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ClrProfiler.Models;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal static class DependencyFactory
    {
        private static readonly Dictionary<Type, ISpanDecorationService> SpanDecorationServiceMap = new Dictionary<Type, ISpanDecorationService>();

        [SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1118:ParameterMustNotSpanMultipleLines", Justification = "Yes it does")]
        static DependencyFactory()
        {
#if !NETSTANDARD2_0
            SpanDecorationServiceMap.Add(
                                         typeof(HttpContextSpanIntegrationDelegate),
                                         new CompositeSpanDecorationService(
                                                                            TagsSpanDecorationService.Instance,
                                                                            TypeSpanDecorationService.Instance,
                                                                            ResourceNameDecorationService.Instance));
#endif
        }

        internal static ISpanDecorationService SpanDecorationService<T>(T forInstance)
            => SpanDecorationService(typeof(T));

        internal static ISpanDecorationService SpanDecorationService(Type forType)
        {
            if (!SpanDecorationServiceMap.TryGetValue(forType, out var service))
            {
                throw new ArgumentOutOfRangeException(nameof(forType));
            }

            return service;
        }
    }
}
