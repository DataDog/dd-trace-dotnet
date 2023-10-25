// <copyright file="JsonAssertionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Originally Based on https://github.com/fluentassertions/fluentassertions.json
// License: https://github.com/fluentassertions/fluentassertions.json/blob/master/LICENSE

using System.Diagnostics;
using System.Diagnostics.Contracts;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.TestHelpers.FluentAssertionsExtensions.Json;

/// <summary>
///     Contains extension methods for JToken assertions.
/// </summary>
[DebuggerNonUserCode]
internal static class JsonAssertionExtensions
{
    /// <summary>
    /// Returns an <see cref="JTokenAssertions"/> object that can be used to assert the current <see cref="JToken"/>.
    /// </summary>
    /// <param name="jToken">JToken</param>
    /// <returns>Token Assertions</returns>
    [Pure]
    public static JTokenAssertions Should(this JToken jToken)
    {
        return new JTokenAssertions(jToken);
    }

    /// <summary>
    /// Returns an <see cref="JTokenAssertions"/> object that can be used to assert the current <see cref="JObject"/>.
    /// </summary>
    /// <param name="jObject">JObject</param>
    /// <returns>Token Assertions</returns>
    [Pure]
    public static JTokenAssertions Should(this JObject jObject)
    {
        return new JTokenAssertions(jObject);
    }

    /// <summary>
    /// Returns an <see cref="JTokenAssertions"/> object that can be used to assert the current <see cref="JValue"/>.
    /// </summary>
    /// <param name="jValue">JValue</param>
    /// <returns>Token Assertions</returns>
    [Pure]
    public static JTokenAssertions Should(this JValue jValue)
    {
        return new JTokenAssertions(jValue);
    }
}
