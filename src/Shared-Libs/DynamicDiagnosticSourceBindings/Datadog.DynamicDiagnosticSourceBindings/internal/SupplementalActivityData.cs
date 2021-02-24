using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenTelemetry.DynamicActivityBinding
{
    internal class SupplementalActivityData
    {
        private Dictionary<string, object> _customProperties = null;

        public ActivityKindStub ActivityKind { get; set; }

        public object GetCustomProperty(string propertyName)
        {
            // We don't check null name here as the dictionary is performing this check anyway.

            if (_customProperties == null)
            {
                return null;
            }

            lock (_customProperties)
            {
                if (false == _customProperties.TryGetValue(propertyName, out object probValue))
                {
                    return null;
                }

                return probValue;
            }
        }

        public void SetCustomProperty(string propertyName, object propertyValue)
        {
            Dictionary<string, object> customProperties = _customProperties;
            if (customProperties == null)
            {
                customProperties = new Dictionary<string, object>();
                Dictionary<string, object> prevProperties = Interlocked.CompareExchange(ref _customProperties, customProperties, null);
                customProperties = prevProperties ?? customProperties;
            }

            lock (customProperties)
            {
                if (propertyValue == null)
                {
                    customProperties.Remove(propertyName);
                }
                else
                {
                    customProperties[propertyName] = propertyValue;
                }
            }
        }
    }
}