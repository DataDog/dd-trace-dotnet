// <copyright file="JsonTokenizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

internal class JsonTokenizer : ITokenizer
{
    private static readonly Regex SourceValueRegex = new(@"(?i)bearer\s+[a-z0-9\._\-]+|token:[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=-]+\.ey[I-L][\w=-]+(\.[\w.+\/=-]+)?|[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY|ssh-rsa\s*[a-z0-9\/\.+]{100,}", RegexOptions.Compiled);

    public List<Range> GetTokens(string value, IntegrationId? integrationId = null)
    {
        var redactedRanges = new List<Range>();

        // Remove new lines from the value by space
        // to get the correct position of the token with the .LinePosition property
        value = value.Replace("\n", " ");

        using var sr = new StringReader(value);
        using var reader = new JsonTextReader(sr);

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                case JsonToken.Integer:
                case JsonToken.Float:
                case JsonToken.Boolean:
                    RedactValue(reader, redactedRanges);
                    break;

                case JsonToken.PropertyName:
                    RedactKey(value, reader, redactedRanges);
                    break;
            }
        }

        return redactedRanges;
    }

    private static void RedactValue(JsonTextReader reader, List<Range> ranges)
    {
        var length = reader.Value!.ToString()!.Length;
        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0; // offset to account for the closing quote
        var start = reader.LinePosition - length - stringOffset;
        ranges.Add(new Range(start, length));
    }

    private static void RedactKey(string value, JsonTextReader reader, List<Range> ranges)
    {
        var propertyName = reader.Value!.ToString()!;
        if (!SourceValueRegex.IsMatch(propertyName))
        {
            return;
        }

        // The current position is at the end of the property name, on the ':' character
        // We need to go back to the end of the property name, on the last double quote
        var end = reader.LinePosition - 1;
        while (value[end] != '"')
        {
            end--;
        }

        if (value[end] != '"')
        {
            // We didn't find the end of the property name, so we can't redact it
            return;
        }

        var length = propertyName.Length;
        var start = end - length;
        ranges.Add(new Range(start, length));
    }
}
