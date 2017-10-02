using OpenTracing;

namespace Datadog.Tracer
{
    internal interface IDatadogTracer : ITracer
    {
        string DefaultServiceName { get; }
        void Write(Span span);
    }
}