// <copyright file="UfcConfigurationParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.FeatureFlags.Agentless;

/// <summary>
/// Reads the JSON:API envelope returned by the agentless endpoint.
/// </summary>
internal static class UfcConfigurationParser
{
    private const string ResourceType = "universal-flag-configuration";

    private const string MalformedError = "Malformed UFC payload";
    private const string ResourceError = "Expected a JSON:API Universal Flag Configuration resource";
    private const string AttributesError = "Expected a Universal Flag Configuration v1 object";

    /// <summary>
    /// Validates a JSON:API Universal Flag Configuration response and returns <c>data.attributes</c>,
    /// which is the document the evaluator consumes. A raw UFC document is rejected, including from
    /// a custom endpoint, so that every source agrees on one wire format.
    /// <para>
    /// The envelope is walked with the reader rather than loaded into a JSON tree, and
    /// <c>data.attributes</c> is deserialized in place, so the payload is read exactly once and no
    /// copy of it is ever held.
    /// </para>
    /// </summary>
    /// <param name="body">The response body. Read straight from the response, so it never has to be held as a string.</param>
    /// <param name="configuration">The parsed configuration.</param>
    /// <param name="error">Why the payload was rejected.</param>
    /// <returns><c>true</c> when the payload matches the contract.</returns>
    public static bool TryParse(TextReader body, [NotNullWhen(true)] out ServerConfiguration? configuration, out string? error)
    {
        configuration = null;
        error = null;

        var sawData = false;
        string? resourceType = null;
        ServerConfiguration? attributes = null;

        try
        {
            // Timestamps stay strings: the model carries createdAt verbatim, and letting Newtonsoft
            // turn it into a date would also make the type check below fail. The reader belongs to
            // the caller, which owns the response it came from.
            using var reader = new JsonTextReader(body) { DateParseHandling = DateParseHandling.None, CloseInput = false, ArrayPool = JsonArrayPool.Shared };
            var serializer = new JsonSerializer { DateParseHandling = DateParseHandling.None };

            if (!reader.Read())
            {
                // Nothing at all, so there is no document to judge against the contract.
                error = MalformedError;
                return false;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                error = ResourceError;
                return false;
            }

            while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
            {
                if ((string?)reader.Value != "data")
                {
                    reader.Skip();
                    continue;
                }

                sawData = true;

                if (!reader.Read())
                {
                    // The document ended where the resource should have been.
                    error = MalformedError;
                    return false;
                }

                if (reader.TokenType != JsonToken.StartObject)
                {
                    error = ResourceError;
                    return false;
                }

                while (reader.Read() && reader.TokenType == JsonToken.PropertyName)
                {
                    switch ((string?)reader.Value)
                    {
                        case "type":
                            if (!reader.Read())
                            {
                                error = MalformedError;
                                return false;
                            }

                            // A type that is not a string cannot identify the resource. Checked on
                            // the token, because a number would otherwise be read as its digits.
                            if (reader.TokenType != JsonToken.String)
                            {
                                error = ResourceError;
                                return false;
                            }

                            resourceType = (string?)reader.Value;
                            break;

                        case "attributes":
                            if (!reader.Read())
                            {
                                error = MalformedError;
                                return false;
                            }

                            if (reader.TokenType != JsonToken.StartObject)
                            {
                                error = AttributesError;
                                return false;
                            }

                            attributes = serializer.Deserialize<ServerConfiguration>(reader);
                            break;

                        default:
                            reader.Skip();
                            break;
                    }
                }
            }

            // A document that ends before the root object closes was truncated in transit, whatever
            // was found in it up to that point.
            if (reader.TokenType != JsonToken.EndObject)
            {
                error = MalformedError;
                return false;
            }
        }
        catch (Exception)
        {
            error = MalformedError;
            return false;
        }

        if (!sawData || resourceType != ResourceType)
        {
            error = ResourceError;
            return false;
        }

        // Every member of the v1 contract has to be there. A member of the wrong shape arrives as
        // null, because the flag collection rejects anything that is not an object and Newtonsoft
        // leaves a member it cannot convert unset.
        if (attributes is null
         || attributes.Format is null
         || attributes.CreatedAt is null
         || attributes.Environment?.Name is null
         || attributes.Flags is null)
        {
            error = AttributesError;
            return false;
        }

        configuration = attributes;
        return true;
    }
}
