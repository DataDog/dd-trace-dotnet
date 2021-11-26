// <copyright file="ManualTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler
{
    internal class ManualTracer : CommonTracer, IDistributedTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ManualTracer));

        private readonly IAutomaticTracer _parent;

        internal ManualTracer(IAutomaticTracer parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _parent.Register(this);
        }

        IScope IDistributedTracer.GetActiveScope()
        {
            var activeTrace = _parent.GetDistributedTrace();

            if (activeTrace is SpanContext)
            {
                // This is a local trace, no need to mock anything
                return null;
            }

            // We don't own the active trace, get the scope from the parent and mock it
            var activeScope = _parent.GetActiveScope();

            if (activeScope is null)
            {
                Log.Warning("Parent tracer owns the active trace, yet the scope is null");
                return null;
            }

            try
            {
                return activeScope.DuckCast<IScope>();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error while trying to ducktype the parent scope");
                return null;
            }
        }

        SpanContext IDistributedTracer.GetSpanContext()
        {
            var values = _parent.GetDistributedTrace();
            if (values is SpanContext spanContext)
            {
                return spanContext;
            }
            else
            {
                return SpanContextPropagator.Instance.Extract(values);
            }
        }

        void IDistributedTracer.SetSpanContext(SpanContext value)
        {
            _parent.SetDistributedTrace(value);
        }

        void IDistributedTracer.LockSamplingPriority()
        {
            _parent.LockSamplingPriority();
        }

        SamplingPriority? IDistributedTracer.TrySetSamplingPriority(SamplingPriority? samplingPriority)
        {
            return (SamplingPriority?)_parent.TrySetSamplingPriority((int?)samplingPriority);
        }
    }
}
