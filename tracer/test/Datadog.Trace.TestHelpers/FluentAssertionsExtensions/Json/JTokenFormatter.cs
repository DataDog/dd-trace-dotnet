// <copyright file="JTokenFormatter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

using Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json.Common;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions.Formatting;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

/// <summary>
/// A <see cref="IValueFormatter"/> for <see cref="JToken" />.
/// </summary>
internal class JTokenFormatter : IValueFormatter
{
    /// <summary>
    /// Indicates whether the current <see cref="IValueFormatter"/> can handle the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value for which to create a <see cref="string"/>.</param>
    /// <returns>
    /// <c>true</c> if the current <see cref="IValueFormatter"/> can handle the specified value; otherwise, <c>false</c>.
    /// </returns>
    public bool CanHandle(object value)
    {
        return value is JToken;
    }

    public void Format(object value, FormattedObjectGraph formattedGraph, FormattingContext context, FormatChild formatChild)
    {
        var jToken = value as JToken;

        if (context.UseLineBreaks)
        {
            var result = jToken?.ToString(Formatting.Indented);
            if (result is not null)
            {
                formattedGraph.AddFragmentOnNewLine(result);
            }
            else
            {
                formattedGraph.AddFragment("<null>");
            }
        }
        else
        {
            formattedGraph.AddFragment(jToken?.ToString().RemoveNewLines() ?? "<null>");
        }
    }
}
