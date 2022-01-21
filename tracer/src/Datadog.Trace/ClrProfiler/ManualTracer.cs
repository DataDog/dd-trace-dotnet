// <copyright file="ManualTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler
{
    internal class ManualTracer : CommonTracer, IDistributedTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ManualTracer));

        private readonly IAutomaticTracer _parent;

        internal ManualTracer(object automaticTracer)
        {
            if (automaticTracer is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(automaticTracer));
            }

            // try the newest interface first and fall back to older ones
            _parent = automaticTracer.DuckAs<IAutomaticTracer2>() ??
                      automaticTracer.DuckCast<IAutomaticTracer>();

            _parent.Register(this);
        }

        bool IDistributedTracer.IsChildTracer => true;

        IScope IDistributedTracer.GetActiveScope()
        {
            var activeTrace = _parent.GetDistributedTrace();

            if (activeTrace is SpanContext)
            {
                // This is a local trace, no need to mock anything
                return null;
            }

            // We don't own the active trace, get the scope from the parent and mock it
            var activeScope = _parent.GetAutomaticActiveScope();

            if (activeScope is null)
            {
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

        IReadOnlyDictionary<string, string> IDistributedTracer.GetSpanContextRaw() => _parent.GetDistributedTrace();

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

        void IDistributedTracer.SetSpanContext(IReadOnlyDictionary<string, string> value)
        {
            _parent.SetDistributedTrace(value);
        }

        SamplingDecision? IDistributedTracer.GetSamplingDecision()
        {
            if (_parent is IAutomaticTracer2 automaticTracer2)
            {
                if (automaticTracer2.GetSamplingDecision(out var priority, out var mechanism, out var rate))
                {
                    return new SamplingDecision(priority, mechanism, rate);
                }

                return null;
            }

            // IAutomaticTracer2 is not available
            var samplingPriority = _parent?.GetSamplingPriority();

            if (samplingPriority != null)
            {
                return new SamplingDecision(samplingPriority.Value, SamplingMechanism.Unknown);
            }

            return null;
        }

        void IDistributedTracer.SetSamplingDecision(SamplingDecision? samplingDecision)
        {
            if (_parent is IAutomaticTracer2 automaticTracer2)
            {
                if (samplingDecision is null)
                {
                    automaticTracer2.ClearSamplingDecision();
                    return;
                }

                var (samplingPriority, samplingMechanism, rate) = (SamplingDecision)samplingDecision;
                automaticTracer2.SetSamplingDecision(samplingPriority, samplingMechanism, rate);
                return;
            }

            // IAutomaticTracer2 is not available
            _parent.SetSamplingPriority(samplingDecision?.Priority);
        }

        string IDistributedTracer.GetRuntimeId() => _parent.GetAutomaticRuntimeId();
    }
}
