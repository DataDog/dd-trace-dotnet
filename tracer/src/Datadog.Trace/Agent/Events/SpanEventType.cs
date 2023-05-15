namespace Datadog.Trace.Agent.Events;

public enum SpanEventType : byte
{
    Unknown = 0,
    StartSpan = 4,
    FinishSpan = 5,
    AddSpanTags = 6,
}
