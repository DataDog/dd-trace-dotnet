using System.Collections.Generic;

namespace Samples.Shared.Web
{
    public class DistributedTracingModel
    {
        public List<SpanIdsModel> Spans { get; set; }

        public void AddSpan(
            string method,
            string serviceName,
            string operationName,
            string resourceName,
            ulong? traceId,
            ulong? spanId)
        {
            if (Spans == null)
            {
                Spans = new List<SpanIdsModel>();
            }

            Spans.Insert(
                0,
                new SpanIdsModel
                {
                    Method = method,
                    ServiceName = serviceName,
                    OperationName = operationName,
                    ResourceName = resourceName,
                    TraceId = traceId,
                    SpanId = spanId
                });
        }
    }

    public class SpanIdsModel
    {
        public string Method { get; set; }
        public string ServiceName { get; set; }
        public string OperationName { get; set; }
        public string ResourceName { get; set; }
        public ulong? TraceId { get; set; }
        public ulong? SpanId { get; set; }
    }
}
