namespace Datadog.Trace.Tagging
{
    internal interface ITags
    {
        string GetTag(string key);

        void SetTag(string key, string value);

        double? GetMetric(string key);

        void SetMetric(string key, double? value);

        int SerializeTo(ref byte[] buffer, int offset);
    }
}
