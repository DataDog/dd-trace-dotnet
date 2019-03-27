using System.Collections.Concurrent;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// A collection of <see cref="IntegrationSettings"/> instances, referenced by name.
    /// </summary>
    public class IntegrationSettingsCollection
    {
        private readonly IConfigurationSource _source;
        private readonly ConcurrentDictionary<string, IntegrationSettings> _settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegrationSettingsCollection"/> class.
        /// </summary>
        /// <param name="source">The <see cref="IConfigurationSource"/> to use when retrieving configuration values.</param>
        public IntegrationSettingsCollection(IConfigurationSource source)
        {
            _source = source;
            _settings = new ConcurrentDictionary<string, IntegrationSettings>();
        }

        /// <summary>
        /// Gets the <see cref="IntegrationSettings"/> for the specified integration.
        /// </summary>
        /// <param name="integrationName">The name of the integration.</param>
        /// <returns>The integration-specific settings for the specified integration.</returns>
        public IntegrationSettings this[string integrationName] =>
            _settings.GetOrAdd(integrationName, name => new IntegrationSettings(name, _source));
    }
}
