namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A base <see cref="IConfigurationSource"/> implementation
    /// for string-only configuration sources.
    /// </summary>
    public abstract class StringConfigurationSource : IConfigurationSource
    {
        /// <inheritdoc />
        public abstract string GetString(string key);

        /// <inheritdoc />
        public virtual int? GetInt32(string key)
        {
            string value = GetString(key);

            return int.TryParse(value, out int result)
                       ? result
                       : (int?)null;
        }

        /// <inheritdoc />
        public double? GetDouble(string key)
        {
            string value = GetString(key);

            return double.TryParse(value, out double result)
                       ? result
                       : (double?)null;
        }

        /// <inheritdoc />
        public virtual bool? GetBool(string key)
        {
            string value = GetString(key);

            return bool.TryParse(value, out bool result)
                       ? result
                       : (bool?)null;
        }
    }
}
