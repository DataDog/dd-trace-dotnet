// <copyright file="ManualTracer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler
{
    internal class ManualTracer : CommonTracer, IDistributedTracer
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ManualTracer));

        private readonly IAutomaticTracer _parent;

        internal ManualTracer(IAutomaticTracer parent)
        {
            if (parent is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(parent));
            }

            _parent = parent;
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

        int? IDistributedTracer.GetSamplingPriority()
        {
            return _parent.GetSamplingPriority();
        }

        void IDistributedTracer.SetSamplingPriority(int? samplingPriority)
        {
            _parent.SetSamplingPriority(samplingPriority);
        }

        string IDistributedTracer.GetRuntimeId() => _parent.GetAutomaticRuntimeId();
    }
}
