// <copyright file="SourceDictionaryConverter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Iast;

/// <summary>
/// Convert <see cref="Datadog.Trace.Iast.Source"/> struct to a dictionary
/// </summary>
internal static class SourceDictionaryConverter
{
    // When not redacted output is:
    // { "origin": "http.request.parameter.name", "name": "name", "value": "value" }
    //
    // When redacted output is:
    // { "origin": "http.request.parameter.name", "name": "name", "redacted": true }

    public static Dictionary<string, object> ToDictionary(this Source source, int maxValueLength)
    {
        var result = new Dictionary<string, object> { { "origin", SourceTypeUtils.GetString(source.Origin) } };

        if (source.Name != null)
        {
            result["name"] = source.Name;
        }

        if (source is { IsRedacted: true, Value: not null })
        {
            result["redacted"] = true;
            TruncationUtils.InsertTruncatableValue(result, "pattern", source.RedactedValue, maxValueLength);
        }
        else if (source.Value != null)
        {
            TruncationUtils.InsertTruncatableValue(result, "value", source.Value, maxValueLength);
        }

        return result;
    }
}
