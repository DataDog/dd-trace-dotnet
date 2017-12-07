using OpenTracing;

namespace Datadog.Trace
{
    internal interface ICodec
    {
        void Inject(SpanContext spanContext, object carrier);

        SpanContext Extract(object carrier);
    }
}