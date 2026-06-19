// <copyright file="JsonTestData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Text;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

internal static class JsonTestData
{
    /// <summary>
    /// Generates deeply nested JSON objects to test max depth constraints
    /// </summary>
    /// <param name="depth">Number of nested levels to create</param>
    /// <returns>JSON string with nested objects</returns>
    public static string GenerateNestedJson(int depth)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < depth; i++)
        {
            sb.Append("{\"level" + i + "\":");
        }

        sb.Append("\"deepest\"");

        for (int i = 0; i < depth; i++)
        {
            sb.Append("}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates JSON array with many elements to test max element constraints
    /// </summary>
    /// <param name="elements">Number of array elements to create</param>
    /// <returns>JSON string with large array</returns>
    public static string GenerateArrayJson(int elements)
    {
        var sb = new StringBuilder();
        sb.Append("[");
        for (int i = 0; i < elements; i++)
        {
            if (i > 0)
            {
                sb.Append(",");
            }

            sb.Append(i);
        }

        sb.Append("]");
        return sb.ToString();
    }

    /// <summary>
    /// Generates JSON object with many properties to test max element constraints
    /// </summary>
    /// <param name="properties">Number of properties to create</param>
    /// <returns>JSON string with many properties</returns>
    public static string GenerateObjectWithManyProperties(int properties)
    {
        var sb = new StringBuilder();
        sb.Append("{");
        for (int i = 0; i < properties; i++)
        {
            if (i > 0)
            {
                sb.Append(",");
            }

            sb.Append($"\"prop{i}\":{i}");
        }

        sb.Append("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates JSON with a very long string value to test max string length constraints
    /// </summary>
    /// <param name="stringLength">Length of string value to create</param>
    /// <returns>JSON string with long string value</returns>
    public static string GenerateLargeStringJson(int stringLength)
    {
        var largeString = new string('a', stringLength);
        return $"{{\"longString\":\"{largeString}\"}}";
    }

    /// <summary>
    /// Generates complex nested structure mixing objects and arrays
    /// </summary>
    /// <returns>Complex JSON for testing mixed types</returns>
    public static string GenerateMixedStructure()
    {
        return @"{
            ""name"": ""test"",
            ""age"": 30,
            ""active"": true,
            ""balance"": 100.50,
            ""tags"": [""tag1"", ""tag2"", ""tag3""],
            ""address"": {
                ""street"": ""123 Main St"",
                ""city"": ""TestCity"",
                ""coordinates"": {
                    ""lat"": 40.7128,
                    ""lon"": -74.0060
                }
            },
            ""scores"": [95, 87, 92, 88],
            ""metadata"": null
        }";
    }
}
