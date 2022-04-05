// <copyright file="JsonUtility.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.Debugger;

public class JsonUtility
{
    /// <summary>
    /// Alphabetize all the properties in a json document.
    /// </summary>
    public static string NormalizeJsonString(string json)
    {
        // Parse json string into JObject.
        var parsedObject = JObject.Parse(json);

        // Sort properties of JObject.
        var normalizedObject = SortPropertiesAlphabetically(parsedObject);

        // Serialize JObject .
        return JsonConvert.SerializeObject(normalizedObject);
    }

    private static JObject SortPropertiesAlphabetically(JObject original)
    {
        var result = new JObject();

        foreach (var property in original.Properties().ToList().OrderBy(p => p.Name))
        {
            if (property.Value is JObject value)
            {
                value = SortPropertiesAlphabetically(value);
                result.Add(property.Name, value);
            }
            else if (property.Value is JArray array)
            {
                var newArray = new JArray();
                foreach (var arrayItem in array)
                {
                    if (arrayItem is JObject obj)
                    {
                        newArray.Add(SortPropertiesAlphabetically(obj));
                    }
                    else
                    {
                        newArray.Add(arrayItem);
                    }
                }

                result.Add(property.Name, newArray);
            }
            else
            {
                result.Add(property.Name, property.Value);
            }
        }

        return result;
    }
}
