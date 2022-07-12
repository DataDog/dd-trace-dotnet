// <copyright file="DictionaryExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>using System.Collections.Generic;

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.Debugger.Helpers;

internal static class DictionaryExtensions
{
    public static string ToDDTagsQueryString(this IDictionary<string, string> keyValues)
    {
        if (keyValues.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var keyValue in keyValues)
        {
            sb.Append(keyValue.Key);
            sb.Append(':');
            sb.Append(keyValue.Value ?? "null");
            sb.Append(',');
        }

        sb.Remove(sb.Length - 1, 1);
        return $"?ddtags={sb}";
    }
}
