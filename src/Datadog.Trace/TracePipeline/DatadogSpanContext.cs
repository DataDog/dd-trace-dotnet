using System;
using Datadog.Trace.Logging;

namespace Datadog.Trace
{
    internal class DatadogSpanContext : IDisposable
    {
        private static readonly ILog Log = LogProvider.For<DatadogSpanContext>();
        private readonly Action<DatadogSpanContext> _disposalTask;

        public DatadogSpanContext(Action<DatadogSpanContext> disposalTask)
        {
            _disposalTask = disposalTask;
        }

        public ulong TraceId { get; set; }

        public ulong ParentSpanId { get; set; }

        public ulong SpanId { get; set; }

        public Guid UniqueId { get; set; }

        public object Span { get; set; }

        public bool IsFinished { get; set; }

        public bool AlivePastTimeToLive { get; set; }

        public DateTime TimeToForceFlush { get; set; } = DateTime.UtcNow.AddMinutes(10);

        public TimeSpan TimeToLiveAfterTraceClose { get; set; } = TimeSpan.FromMinutes(2);

        public void Dispose()
        {
            try
            {
                _disposalTask(this);
            }
            catch (Exception ex)
            {
                // No exceptions in dispose
                Log.Error(ex, $"Error when disposing {nameof(DatadogSpanContext)}.");
            }
        }
    }
}
