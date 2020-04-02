#if !NET45
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Datadog.Trace.ExtensionMethods;
using MessagePack;

namespace Datadog.Trace.Agent
{
    [MessagePackObject]
    [DebuggerDisplay("TraceId={TraceId}, SpanId={SpanId}, Service={Service}, Name={Name}, Resource={Resource}")]
    internal struct SerializableSpan
    {
        [Key("trace_id")]
        public ulong TraceId;

        [Key("span_id")]
        public ulong SpanId;

        [Key("name")]
        public string Name;

        [Key("resource")]
        public string Resource;

        [Key("service")]
        public string Service;

        [Key("type")]
        public string Type;

        [Key("start")]
        public long Start;

        [Key("duration")]
        public long Duration;

        [Key("parent_id")]
        public ulong? ParentId;

        [Key("error")]
        public byte Error;

        [Key("meta")]
        public Dictionary<string, string> Tags;

        [Key("metrics")]
        public Dictionary<string, double> Metrics;

        public static SerializableSpan[][] ConvertTraces(Span[][] traces)
        {
            var serializableTraces = new SerializableSpan[traces.Length][];

            for (int traceIndex = 0; traceIndex < traces.Length; traceIndex++)
            {
                Span[] trace = traces[traceIndex];
                var serializableTrace = new SerializableSpan[trace.Length];

                for (int spanIndex = 0; spanIndex < trace.Length; spanIndex++)
                {
                    serializableTrace[spanIndex] = FromSpan(trace[spanIndex]);
                }

                serializableTraces[traceIndex] = serializableTrace;
            }

            return serializableTraces;
        }

        public static SerializableSpan FromSpan(Span span)
        {
            return new SerializableSpan
                   {
                       TraceId = span.TraceId,
                       SpanId = span.SpanId,
                       Name = span.OperationName,
                       Resource = span.ResourceName,
                       Service = span.ServiceName,
                       Type = span.Type,
                       Start = span.StartTime.ToUnixTimeNanoseconds(),
                       Duration = span.Duration.ToNanoseconds(),
                       ParentId = span.Context?.ParentId,
                       Error = (byte)(span.Error ? 1 : 0),
                       Tags = span.Tags,
                       Metrics = span.Metrics
                   };
        }
    }
}
#endif
