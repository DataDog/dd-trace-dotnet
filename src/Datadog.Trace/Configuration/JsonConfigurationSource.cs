using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.Configuration
{
    public class JsonConfigurationSource : IConfigurationSource
    {
        private readonly JObject _configuration;

        public static JsonConfigurationSource LoadFile(string filename)
        {
            string json = File.ReadAllText(filename);
            return new JsonConfigurationSource(json);
        }

        public JsonConfigurationSource(string json)
        {
            _configuration = (JObject)JsonConvert.DeserializeObject(json);
        }

        public T GetValue<T>(string key)
        {
            JToken token = _configuration.SelectToken(key, errorWhenNoMatch: false);
            return token == null ? default(T) : token.Value<T>();
        }

        string IConfigurationSource.GetString(string key)
        {
            return GetValue<string>(key);
        }

        int? IConfigurationSource.GetInt32(string key)
        {
            return GetValue<int?>(key);
        }

        bool? IConfigurationSource.GetBool(string key)
        {
            return GetValue<bool?>(key);
        }
    }
}
