namespace Datadog.Trace.Interfaces
{
    internal interface ISpan
    {
        bool Error { get; set; }

        string ResourceName { get; set; }

        string Type { get; set; }

        string GetTag(string key);

        void Tag(string key, string value);
    }
}
