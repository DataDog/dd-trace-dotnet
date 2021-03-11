using System;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal class BombardierRawConverter : IResultConverter
    {
        public BombardierRawConverter(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool CanConvert(string name)
            => Name == name;

        public void SetToSpan(Span span, string sanitizedName, object value)
        {
            if (value is JArray valueArray)
            {
                for (var i = 0; i < valueArray.Count; i++)
                {
                    string prefix = sanitizedName + ".";

                    if (valueArray.Count != 1)
                    {
                        prefix += i + ".";
                    }

                    SetSpan(span, prefix, valueArray[i]);
                }
            }
        }

        private void SetSpan(Span span, string prefix, JToken token)
        {
            foreach (var item in token)
            {
                if (item is JProperty itemProperty)
                {
                    string name = itemProperty.Name;
                    if (double.TryParse(name, out _))
                    {
                        // avoid the string to number conversion in the ui.
                        if (prefix.Contains("percentiles", StringComparison.OrdinalIgnoreCase))
                        {
                            name = "p" + name;
                        }
                        else
                        {
                            name = "_" + name;
                        }
                    }

                    if (itemProperty.Value is JObject valueObject)
                    {
                        SetSpan(span, prefix + name + ".", valueObject);
                    }
                    else if (double.TryParse(itemProperty.Value.ToString(), out var itemValue))
                    {
                        span.SetMetric(prefix + name, itemValue);
                    }
                    else
                    {
                        span.SetTag(prefix + name, itemProperty.Value.ToString());
                    }
                }
            }
        }
    }
}
