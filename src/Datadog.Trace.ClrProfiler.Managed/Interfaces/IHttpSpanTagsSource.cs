namespace Datadog.Trace.ClrProfiler.Interfaces
{
    internal interface IHttpSpanTagsSource
    {
        string GetHttpMethod();

        string GetHttpHost();

        string GetHttpUrl();
    }
}
