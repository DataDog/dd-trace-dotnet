// <copyright file="TruncatorTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.TraceProcessors
{
    internal class TruncatorTraceProcessor : ITraceProcessor
    {
        // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/agent/truncator.go

        // Values from: https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/traceutil/truncate.go#L22-L27
        // MaxResourceLen the maximum length a span resource can have
        internal const int MaxResourceLen = 5000;
        // MaxMetaKeyLen the maximum length of metadata key
        internal const int MaxMetaKeyLen = 200;
        // MaxMetaValLen the maximum length of metadata value
        internal const int MaxMetaValLen = 25000;
        // MaxMetricsKeyLen the maximum length of a metric name key
        internal const int MaxMetricsKeyLen = MaxMetaKeyLen;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TruncatorTraceProcessor>();
        private readonly TruncatorTagsProcessor truncatorTagsProcessor = new TruncatorTagsProcessor();

        public TruncatorTraceProcessor()
        {
            Log.Information("TruncatorTraceProcessor initialized.");
        }

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            foreach (var span in trace)
            {
                // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/agent/truncator.go#L17-L21
                span.ResourceName = TruncateResource(span.ResourceName);

                // Set the tags processor
                if (span.Tags is TagsList tagsList)
                {
                    tagsList.AddTagProcessor(truncatorTagsProcessor);
                }
            }

            return trace;
        }

        // https://github.com/DataDog/datadog-agent/blob/0454961e636342c9fbab9e561e6346ae804679a9/pkg/trace/traceutil/truncate.go#L28-L32
        internal static string TruncateResource(string r)
        {
            if (TraceUtil.TruncateUTF8(ref r, MaxResourceLen))
            {
                Log.Information("span.truncate: truncated `Resource` (max {maxResourceLen} chars): {resource}", MaxResourceLen, r);
            }

            return r;
        }

        private class TruncatorTagsProcessor : ITagProcessor
        {
            // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/agent/truncator.go#L26-L44
            public void ProcessMeta(ref string key, ref string value)
            {
                if (TraceUtil.TruncateUTF8(ref key, MaxMetaKeyLen))
                {
                    key += "...";
                    Log.Information("span.truncate: truncating `Meta` key (max {maxMetaKeyLen} chars): {key}", MaxMetaKeyLen, key);
                }

                if (TraceUtil.TruncateUTF8(ref value, MaxMetaValLen))
                {
                    value += "...";
                    Log.Information("span.truncate: truncating `Meta` value (max {maxMetaValLen} chars): {value}", MaxMetaValLen, value);
                }
            }

            // https://github.com/DataDog/datadog-agent/blob/main/pkg/trace/agent/truncator.go#L45-L53
            public void ProcessMetric(ref string key, ref double value)
            {
                if (TraceUtil.TruncateUTF8(ref key, MaxMetricsKeyLen))
                {
                    key += "...";
                    Log.Information("span.truncate: truncating `Metrics` key (max {maxMetricsKeyLen} chars): {key}", MaxMetricsKeyLen, key);
                }
            }
        }
    }
}
