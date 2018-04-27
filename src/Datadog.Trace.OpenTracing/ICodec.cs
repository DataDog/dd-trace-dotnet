namespace Datadog.Trace
{
    internal interface ICodec
    {
        void Inject(OpenTracingSpanContext spanContext, object carrier);

        OpenTracingSpanContext Extract(object carrier);
    }
}