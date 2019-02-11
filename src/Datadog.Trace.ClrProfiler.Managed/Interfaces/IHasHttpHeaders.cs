namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface IHasHttpHeaders
    {
        string GetHeaderValue(string headerName);
    }
}
