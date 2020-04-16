using System.Collections.Generic;
using System.Diagnostics;
using MessagePack;

namespace MockTraceAgent
{
    [MessagePackObject]
    [DebuggerDisplay("TraceId={TraceId}, SpanId={SpanId}, Service={Service}, Name={Name}, Resource={Resource}")]
    public class MockSpan
    {
        [Key("trace_id")]
        public ulong TraceId { get; set; }

        [Key("span_id")]
        public ulong SpanId { get; set; }

        [Key("name")]
        public string Name { get; set; }

        [Key("resource")]
        public string Resource { get; set; }

        [Key("service")]
        public string Service { get; set; }

        [Key("type")]
        public string Type { get; set; }

        [Key("start")]
        public long Start { get; set; }

        [Key("duration")]
        public long Duration { get; set; }

        [Key("parent_id")]
        public ulong? ParentId { get; set; }

        [Key("error")]
        public byte Error { get; set; }

        [Key("meta")]
        public Dictionary<string, string> Tags { get; set; }

        [Key("metrics")]
        public Dictionary<string, double> Metrics { get; set; }
    }
}
