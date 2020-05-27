namespace Datadog.Trace
{
    internal interface ITraceContextStrategy
    {
        void Write(Span[] span);

        SamplingPriority GetSamplingPriority(Span span);
    }
}
