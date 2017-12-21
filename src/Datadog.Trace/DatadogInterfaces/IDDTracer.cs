namespace Datadog.Trace
{
    public interface IDDTracer
    {
        IDDSpanBuilder BuildSpan(string operationName);
    }
}
