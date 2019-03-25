using System.Collections.Specialized;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided <see cref="NameValueCollection"/>.
    /// </summary>
    public class NameValueConfigurationSource : StringConfigurationSource
    {
        private readonly NameValueCollection _nameValueCollection;

        /// <summary>
        /// Initializes a new instance of the <see cref="NameValueConfigurationSource"/> class
        /// that wraps the specified <see cref="NameValueCollection"/>.
        /// </summary>
        /// <param name="nameValueCollection">The collection that will be wrapped by this configuration source.</param>
        public NameValueConfigurationSource(NameValueCollection nameValueCollection)
        {
            _nameValueCollection = nameValueCollection;
        }

        /// <inheritdoc />
        public override string GetString(string key)
        {
            return _nameValueCollection[key];
        }
    }
}
