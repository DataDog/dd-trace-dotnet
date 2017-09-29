using OpenTracing;

namespace Datadog.Tracer
{
    internal interface IDatadogTracer : ITracer
    {
        void Write(Span span);
    }
}