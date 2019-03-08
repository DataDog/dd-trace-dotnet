using System.Collections.Generic;

namespace Samples.Shared.Web
{
    public class DistributedTracingModel
    {
        public List<SpanIdsModel> Spans { get; set; }

        public void AddSpan(string method, ulong? traceId, ulong? spanId)
        {
            if (Spans == null)
            {
                Spans = new List<SpanIdsModel>();
            }

            Spans.Add(
                new SpanIdsModel
                {
                    Method = method,
                    TraceId = traceId,
                    SpanId = spanId
                });
        }
    }

    public class SpanIdsModel
    {
        public string Method { get; set; }
        public ulong? TraceId { get; set; }
        public ulong? SpanId { get; set; }
    }
}
