// <copyright file="DistributedTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.DuckTyping;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1600 // Elements should be documented
#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1204 // Static elements should appear before instance elements

namespace Datadog.Trace.ClrProfiler
{
    public interface IDistributedTracer
    {
        SpanContext GetSpanContext();

        void SetSpanContext(SpanContext value);
    }

    public interface IAutomaticTracer
    {
        object GetDistributedTrace();

        void SetDistributedTrace(object trace);

        // bool TrySetSamplingPriority(int samplingPriority);
    }

    public class AutomaticTracer : IAutomaticTracer, IDistributedTracer
    {
        private static readonly AsyncLocal<IReadOnlyDictionary<string, string>> DistributedTrace = new();

        private bool _multipleTracers;

        internal AutomaticTracer()
        {
        }

        public SpanContext GetSpanContext()
        {
            if (_multipleTracers)
            {
                return SpanContextPropagator.Instance.Extract(DistributedTrace.Value);
            }

            return null;
        }

        public void SetSpanContext(SpanContext value)
        {
            // Locally setting the SpanContext, no need to do anything
        }

        /// <summary>
        /// Gets the internal distributed trace object
        /// </summary>
        /// <returns>Shared distributed trace object instance</returns>
        public object GetDistributedTrace()
        {
            return Tracer.Instance.ActiveScope?.Span.Context;
        }

        /// <summary>
        /// Sets the internal distributed trace object
        /// </summary>
        /// <param name="value">Shared distributed trace object instance</param>
        public void SetDistributedTrace(object value)
        {
            _multipleTracers = true;
            DistributedTrace.Value = value as IReadOnlyDictionary<string, string>;
        }
    }

    public class ManualTracer : IDistributedTracer
    {
        private readonly IAutomaticTracer _parent;

        internal ManualTracer(IAutomaticTracer parent)
        {
            _parent = parent;
        }

        public SpanContext GetSpanContext()
        {
            var values = _parent.GetDistributedTrace();

            return SpanContextPropagator.Instance.Extract(values as IReadOnlyDictionary<string, string>);
        }

        public void SetSpanContext(SpanContext value)
        {
            _parent.SetDistributedTrace(value);
        }
    }

    public static class DistributedTracer
    {
        public static IDistributedTracer Instance { get; } = BuildDistributedTracer();

        public static object GetDistributedTracer() => Instance;

        private static IDistributedTracer BuildDistributedTracer()
        {
            var parent = GetParent();

            if (parent == null)
            {
                return new AutomaticTracer();
            }

            var parentTracer = parent.DuckAs<IAutomaticTracer>();

            return new ManualTracer(parentTracer);
        }

        public static object GetParent()
        {
            // TODO: To be rewritten by the profiler
            return null;
        }
    }
}
