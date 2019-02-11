namespace Datadog.Trace.Interfaces
{
    internal interface ISpan<out T> : ISpan
    {
        T SetTag(string key, string value);
    }

    internal interface ISpan
    {
        string ResourceName { get; set; }

        string Type { get; set; }

        void Tag(string key, string value);

        string GetTag(string key);
    }
}
