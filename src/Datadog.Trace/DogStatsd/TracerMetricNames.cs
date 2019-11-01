namespace Datadog.Trace.DogStatsd
{
    internal static class TracerMetricNames
    {
        public static class Api
        {
            /// <summary>
            /// Count: Total number of API requests made
            /// </summary>
            public const string Requests = "datadog.tracer.api.requests";

            /// <summary>
            /// Count: Count of API responses.
            /// This metric has an additional tag of "status: {code}" to group the responses by the HTTP response code.
            /// This is different from <seealso cref="Errors"/> in that this is all HTTP responses
            /// regardless of status code, and <seealso cref="Errors"/> is exceptions raised from making an API call.
            /// </summary>
            public const string Responses = "datadog.tracer.api.responses";

            /// <summary>
            /// Count: Total number of exceptions raised by API calls.
            /// This is different from receiving a 4xx or 5xx response.
            /// It is a "timeout error" or something from making the API call.
            /// </summary>
            public const string Errors = "datadog.tracer.api.errors";
        }

        public static class Queue
        {
            /// <summary>
            /// Count: Total number of traces pushed into the queue, where "accepted - dropped = total to be flushed"
            /// </summary>
            public const string EnqueuedTraces = "datadog.tracer.queue.accepted";

            /// <summary>
            /// Count: Total number of spans pushed into the queue
            /// </summary>
            public const string EnqueuedSpans = "datadog.tracer.queue.accepted_lengths";

            /// <summary>
            /// Count: Total size in bytes of traces pushed into the queue
            /// </summary>
            public const string EnqueuedBytes = "datadog.tracer.queue.accepted_size";

            /// <summary>
            /// Count: Total number of traces dropped by the queue.
            /// This is the number of traces attempted to write into the queue above the max
            /// (e.g. more than 1k traces, we start dropping traces)
            /// </summary>
            public const string DroppedTraces = "datadog.tracer.queue.dropped";

            /// <summary>
            /// Gauge: Number of traces pulled from the queue for flushing (should be between zero and queue.max_length)
            /// </summary>
            public const string DequeuedTraces = "datadog.tracer.queue.length";

            /// <summary>
            /// Gauge: Total number of spans pulled from the queue for flushing
            /// </summary>
            public const string DequeuedSpans = "datadog.tracer.queue.spans";

            /// <summary>
            /// Gauge: Size in bytes of traces pulled from the queue for flushing
            /// </summary>
            public const string DequeuedBytes = "datadog.tracer.queue.size";

            /// <summary>
            /// Gauge: The maximum number of traces buffered by the background writer (this is static at 1k for now)
            /// </summary>
            public const string MaxCapacity = "datadog.tracer.queue.max_length";
        }
    }
}
