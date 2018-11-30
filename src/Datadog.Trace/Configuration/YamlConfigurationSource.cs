using System.IO;
using YamlDotNet.Serialization;

namespace Datadog.Trace.Configuration
{
    /// <summary>
    /// Represents a configuration source that retrieves
    /// values from the provided YAML string.
    /// </summary>
    public class YamlConfigurationSource : JsonConfigurationSource
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="YamlConfigurationSource"/>
        /// class with the specified YAML string.
        /// </summary>
        /// <param name="yaml">A YAML string that contains configuration values.</param>
        public YamlConfigurationSource(string yaml)
            : base(ConvertToJson(yaml))
        {
        }

        private static string ConvertToJson(string yaml)
        {
            var deserializer = new Deserializer();
            object document = deserializer.Deserialize(new StringReader(yaml));

            var serializer = new SerializerBuilder().JsonCompatible().Build();
            return serializer.Serialize(document);
        }
    }
}
