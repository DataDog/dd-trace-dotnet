// <copyright file="TruncatorTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.TraceProcessors
{
    internal class TruncatorTraceProcessor : ITraceProcessor
    {
        // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/agent/truncator.go

        // MaxResourceLen the maximum length a span resource can have
        private const int MaxResourceLen = 5000;
        // MaxMetaKeyLen the maximum length of metadata key
        private const int MaxMetaKeyLen = 200;
        // MaxMetaValLen the maximum length of metadata value
        private const int MaxMetaValLen = 25000;
        // MaxMetricsKeyLen the maximum length of a metric name key
        private const int MaxMetricsKeyLen = MaxMetaKeyLen;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TruncatorTraceProcessor>();

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            foreach (var span in trace)
            {
                // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/agent/truncator.go#L17-L21
                var resourceName = span.ResourceName;
                if (TraceUtil.TruncateUTF8(ref resourceName, MaxResourceLen))
                {
                    span.ResourceName = resourceName;
                    Log.Debug("span.truncate: truncated `Resource` (max {maxResourceLen} chars): {resource}", MaxResourceLen, span.ResourceName);
                }
            }

            return trace;
        }
    }
}
