namespace Datadog.Trace.OpenTracing
{
    internal interface ICodec
    {
        void Inject(OpenTracingSpanContext spanContext, object carrier);

        OpenTracingSpanContext Extract(object carrier);
    }
}