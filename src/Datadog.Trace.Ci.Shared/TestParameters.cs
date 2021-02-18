using System.Collections.Generic;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci
{
    internal class TestParameters
    {
        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; }

        [JsonProperty("arguments")]
        public Dictionary<string, object> Arguments { get; set; }

        internal string ToJSON()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
