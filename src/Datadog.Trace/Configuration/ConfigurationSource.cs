namespace Datadog.Trace.Configuration
{
    public abstract class ConfigurationSource : IConfigurationSource
    {
        public abstract string GetString(string key);

        public virtual int? GetInt32(string key)
        {
            string value = GetString(key);
            return int.TryParse(value, out int result) ? result : (int?)null;
        }

        public virtual bool? GetBool(string key)
        {
            string value = GetString(key);
            return bool.TryParse(value, out bool result) ? result : (bool?)null;
        }
    }
}
