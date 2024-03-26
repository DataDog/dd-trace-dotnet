// <copyright file="TruncatorTraceProcessor.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Processors
{
    internal class TruncatorTraceProcessor : ITraceProcessor
    {
        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/agent/truncator.go

        // Values from: https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate.go#L21-L28
        // MaxResourceLen the maximum length a span resource can have
        internal const int MaxResourceLen = 5000;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<TruncatorTraceProcessor>();
        private readonly TruncatorTagsProcessor truncatorTagsProcessor = new();

        public TruncatorTraceProcessor()
        {
            Log.Information("TruncatorTraceProcessor initialized.");
        }

        public ArraySegment<Span> Process(ArraySegment<Span> trace)
        {
            for (var i = trace.Offset; i < trace.Count + trace.Offset; i++)
            {
                trace.Array![i] = Process(trace.Array[i]);
            }

            return trace;
        }

        public Span Process(Span span)
        {
            // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/agent/truncator.go#L17-L21
            span.ResourceName = TruncateResource(span.ResourceName);

            return span;
        }

        public ITagProcessor GetTagProcessor()
        {
            return truncatorTagsProcessor;
        }

        // https://github.com/DataDog/datadog-agent/blob/eac2327c5574da7f225f9ef0f89eaeb05ed10382/pkg/trace/traceutil/truncate.go#L30-L34
        internal static string TruncateResource(string r)
        {
            if (TraceUtil.TruncateUTF8(ref r, MaxResourceLen))
            {
                Log.Debug<int, string>("span.truncate: truncated `Resource` (max {MaxResourceLen} chars): {Resource}", MaxResourceLen, r);
            }

            return r;
        }
    }
}
