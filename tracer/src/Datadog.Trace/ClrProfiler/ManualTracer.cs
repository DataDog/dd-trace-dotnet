// <copyright file="ManualTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;

namespace Datadog.Trace.ClrProfiler
{
    internal class ManualTracer : CommonTracer, IDistributedTracer
    {
        private readonly IAutomaticTracer _parent;

        internal ManualTracer(IAutomaticTracer parent)
        {
            _parent = parent ?? throw new ArgumentNullException(nameof(parent));
            _parent.Register(this);
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
