// <copyright file="UfcConfigurationParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// Reads the JSON:API envelope returned by the agentless endpoint.
/// </summary>
internal static class UfcConfigurationParser
{
    private const string ResourceType = "universal-flag-configuration";

    /// <summary>
    /// Validates a JSON:API Universal Flag Configuration response and returns <c>data.attributes</c>,
    /// which is the document the evaluator consumes. A raw UFC document is rejected, including from
    /// a custom endpoint, so that every source agrees on one wire format.
    /// </summary>
    /// <param name="body">The response body.</param>
    /// <param name="configuration">The parsed configuration.</param>
    /// <param name="error">Why the payload was rejected.</param>
    /// <returns><c>true</c> when the payload matches the contract.</returns>
    public static bool TryParse(string? body, [NotNullWhen(true)] out ServerConfiguration? configuration, out string? error)
    {
        configuration = null;
        error = null;

        JToken payload;
        try
        {
            using var stringReader = new StringReader(body ?? string.Empty);

            // Timestamps stay strings: the model carries createdAt verbatim, and letting Newtonsoft
            // turn it into a date would also make the type check below fail.
            using var jsonReader = new JsonTextReader(stringReader) { DateParseHandling = DateParseHandling.None };
            payload = JToken.ReadFrom(jsonReader);
        }
        catch (Exception)
        {
            error = "Malformed UFC payload";
            return false;
        }

        if (payload is not JObject
         || payload["data"] is not JObject data
         || data["type"]?.Value<string>() != ResourceType)
        {
            error = "Expected a JSON:API Universal Flag Configuration resource";
            return false;
        }

        if (data["attributes"] is not JObject attributes
         || attributes["format"]?.Type != JTokenType.String
         || attributes["createdAt"]?.Type != JTokenType.String
         || attributes["environment"] is not JObject environment
         || environment["name"]?.Type != JTokenType.String
         || attributes["flags"] is not JObject)
        {
            error = "Expected a Universal Flag Configuration v1 object";
            return false;
        }

        try
        {
            configuration = attributes.ToObject<ServerConfiguration>();
        }
        catch (Exception)
        {
            configuration = null;
        }

        if (configuration is null)
        {
            error = "Expected a Universal Flag Configuration v1 object";
            return false;
        }

        return true;
    }
}
