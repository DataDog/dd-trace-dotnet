using Newtonsoft.Json;

namespace ServiceBus.Minimal.MassTransit.Common
{
    internal static class JsonHelper
    {
        public static T Deserialize<T>(string json) where T : class
        {
            return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<T>(json);
        }

        public static string Serialize<T>(T obj) where T : class
        {
            return obj == null ? null : JsonConvert.SerializeObject(obj);
        }
    }
}
