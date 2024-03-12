// <copyright file="JsonTokenizer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Datadog.Trace.Configuration;
using Datadog.Trace.Vendors.Newtonsoft.Json;

#nullable enable

namespace Datadog.Trace.Iast.SensitiveData;

internal class JsonTokenizer : ITokenizer
{
    private const string SourceValueRegexString = @"(?i)bearer\s+[a-z0-9\._\-]+|token:[a-z0-9]{13}|gh[opsu]_[0-9a-zA-Z]{36}|ey[I-L][\w=-]+\.ey[I-L][\w=-]+(\.[\w.+\/=-]+)?|[\-]{5}BEGIN[a-z\s]+PRIVATE\sKEY[\-]{5}[^\-]+[\-]{5}END[a-z\s]+PRIVATE\sKEY|ssh-rsa\s*[a-z0-9\/\.+]{100,}";

    private Regex _sourceValueRegex;

    public JsonTokenizer(TimeSpan timeout)
    {
        _sourceValueRegex = new Regex(SourceValueRegexString, RegexOptions.Compiled | RegexOptions.IgnoreCase, timeout);
    }

    public List<Range> GetTokens(Evidence evidence, IntegrationId? integrationId = null)
    {
        var value = evidence.Value;
        if (value is null) { return []; }

        // Remove new lines from the value by space
        // to get the correct position of the token with the .LinePosition property
        value = value.Replace("\n", " ");

        var redactedRanges = new List<Range>();
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
        var readerValue = reader.Value?.ToString();
        if (readerValue is null)
        {
            return;
        }

        var length = readerValue.Length;
        var stringOffset = reader.TokenType == JsonToken.String ? 1 : 0; // offset to account for the closing quote
        var start = reader.LinePosition - length - stringOffset;
        ranges.Add(new Range(start, length));
    }

    private void RedactKey(string value, JsonTextReader reader, List<Range> ranges)
    {
        var readerValue = reader.Value?.ToString();
        if (readerValue is null || !_sourceValueRegex.IsMatch(readerValue))
        {
            return;
        }

        // Guards to not iterate over the previous ranges in case of a malformed JSON
        var length = readerValue.Length;
        Range? lastRange = ranges.LastOrDefault();
        var maxLastPosition = lastRange?.Start + lastRange?.Length;

        // The current position is at the end of the property name, on the ':' character
        // We need to go back to the end of the property name, on the last double quote
        var end = reader.LinePosition - 1;
        while (end > maxLastPosition && value[end] != '"')
        {
            end--;
        }

        if (end <= maxLastPosition || value[end] != '"')
        {
            // We didn't find the end of the property name, so we can't redact it
            return;
        }

        var start = end - length;
        ranges.Add(new Range(start, length));
    }
}
