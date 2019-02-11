namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface IHttpSpanDecoratable : IHasHttpUrl, IHasHttpHeaders, IHasHttpMethod
    {
    }
}
