namespace Datadog.Trace.Tagging
{
    internal interface IHasStatusCode
    {
        string HttpStatusCode { get; set; }
    }
}
