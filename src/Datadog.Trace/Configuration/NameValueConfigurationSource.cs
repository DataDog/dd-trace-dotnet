using System.Collections.Specialized;

#if NET45 || NET46

namespace Datadog.Trace.Configuration
{
    public class NameValueConfigurationSource : ConfigurationSource
    {
        private readonly NameValueCollection _nameValueCollection;

        public NameValueConfigurationSource(NameValueCollection nameValueCollection)
        {
            _nameValueCollection = nameValueCollection;
        }

        public override string GetString(string key)
        {
            return _nameValueCollection[key];
        }
    }
}

#endif

