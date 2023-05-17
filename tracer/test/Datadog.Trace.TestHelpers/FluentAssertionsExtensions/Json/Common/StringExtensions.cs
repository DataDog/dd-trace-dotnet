// <copyright file="StringExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json.Common;

internal static class StringExtensions
{
    /// <summary>
    /// Replaces all characters that might conflict with formatting placeholders with their escaped counterparts.
    /// </summary>
    public static string EscapePlaceholders(this string value) =>
        value.Replace("{", "{{").Replace("}", "}}");

    public static string RemoveNewLines(this string @this)
    {
        return @this.Replace("\n", string.Empty).Replace("\r", string.Empty).Replace("\\r\\n", string.Empty);
    }
}
