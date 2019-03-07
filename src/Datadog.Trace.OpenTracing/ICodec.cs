namespace Datadog.Trace.OpenTracing
{
    internal interface ICodec
    {
        void Inject(global::OpenTracing.ISpanContext spanContext, object carrier);

        global::OpenTracing.ISpanContext Extract(object carrier);
    }
}
