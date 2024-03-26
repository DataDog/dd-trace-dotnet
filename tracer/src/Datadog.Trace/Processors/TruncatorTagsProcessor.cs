// <copyright file="TruncatorTagsProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Logging;

namespace Datadog.Trace.Processors
{
    internal class TruncatorTagsProcessor : ITagProcessor
    {
        // MaxMetaKeyLen the maximum length of metadata key
        internal const int MaxMetaKeyLen = 200;

        // MaxMetaValLen the maximum length of metadata value
        internal const int MaxMetaValLen = 25000;

        // MaxMetricsKeyLen the maximum length of a metric name key
        internal const int MaxMetricsKeyLen = MaxMetaKeyLen;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TruncatorTagsProcessor>();

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/agent/truncator.go#L26-L44
        public void ProcessMeta(ref string key, ref string value)
        {
            if (TraceUtil.TruncateUTF8(ref key, MaxMetaKeyLen))
            {
                key += "...";
                Log.Debug<int, string>("span.truncate: truncating `Meta` key (max {MaxMetaKeyLen} chars): {Key}", MaxMetaKeyLen, key);
            }

            if (TraceUtil.TruncateUTF8(ref value, MaxMetaValLen))
            {
                value += "...";
                Log.Debug<int, string>("span.truncate: truncating `Meta` value (max {MaxMetaValLen} chars): {Value}", MaxMetaValLen, value);
            }
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/agent/truncator.go#L45-L53
        public void ProcessMetric(ref string key, ref double value)
        {
            if (TraceUtil.TruncateUTF8(ref key, MaxMetricsKeyLen))
            {
                key += "...";
                Log.Debug<int, string>("span.truncate: truncating `Metrics` key (max {MaxMetricsKeyLen} chars): {Key}", MaxMetricsKeyLen, key);
            }
        }
    }
}
