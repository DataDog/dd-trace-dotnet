// <copyright file="JsonAssertionExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

/// Based on https://github.com/fluentassertions/fluentassertions.json

using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.Tests.Util.JsonAssertions;

internal static class JsonAssertionExtensions
{
    /// <summary>
    ///     Returns an <see cref="JTokenAssertions"/> object that can be used to assert the current <see cref="JToken"/>.
    /// </summary>
    public static JTokenAssertions JsonShould(this JToken jToken)
    {
        return new JTokenAssertions(jToken);
    }

    /// <summary>
    ///     Returns an <see cref="JTokenAssertions"/> object that can be used to assert the current <see cref="JObject"/>.
    /// </summary>
    public static JTokenAssertions JsonShould(this JObject jObject)
    {
        return new JTokenAssertions(jObject);
    }

    /// <summary>
    ///     Returns an <see cref="JTokenAssertions"/> object that can be used to assert the current <see cref="JValue"/>.
    /// </summary>
    public static JTokenAssertions JsonShould(this JValue jValue)
    {
        return new JTokenAssertions(jValue);
    }
}
