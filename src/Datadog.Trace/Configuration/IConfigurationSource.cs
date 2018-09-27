namespace Datadog.Trace.Configuration
{
    public interface IConfigurationSource
    {
        string GetString(string key);

        int? GetInt32(string key);

        bool? GetBool(string key);
    }
}
