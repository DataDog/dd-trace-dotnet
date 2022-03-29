using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ServiceBus.Minimal.MassTransit.Common
{
    /// <summary>
    /// Converts complex field to/from JSON string.
    /// </summary>
    /// <typeparam name="T">Model field type.</typeparam>
    /// <remarks>See more: https://docs.microsoft.com/en-us/ef/core/modeling/value-conversions </remarks>
    public class JsonValueConverter<T> : ValueConverter<T, string> where T : class
    {
        public JsonValueConverter(ConverterMappingHints hints = default) :
          base(v => JsonHelper.Serialize(v), v => JsonHelper.Deserialize<T>(v), hints)
        { }
    }
}
