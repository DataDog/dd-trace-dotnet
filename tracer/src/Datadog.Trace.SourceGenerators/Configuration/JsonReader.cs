  // <copyright file="JsonReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.SourceGenerators.Configuration;

/// <summary>
/// Json reader as we can't really use vendored libraries here, for framework compatibility reasons
/// </summary>
internal static class JsonReader
{
    internal static string ExtractJsonObjectSection(string json, string sectionName)
    {
        var searchPattern = $"\"{sectionName}\":";
        var startIndex = json.IndexOf(searchPattern, StringComparison.Ordinal);
        if (startIndex == -1)
        {
            return string.Empty;
        }

        // Move to the start of the object value
        startIndex += searchPattern.Length;

        // Skip whitespace to find the opening brace
        while (startIndex < json.Length && char.IsWhiteSpace(json[startIndex]))
        {
            startIndex++;
        }

        if (startIndex >= json.Length || json[startIndex] != '{')
        {
            return string.Empty;
        }

        // Find the matching closing brace
        var braceCount = 0;
        var endIndex = startIndex;
        var inString = false;
        var escapeNext = false;

        for (int i = startIndex; i < json.Length; i++)
        {
            var ch = json[i];

            if (escapeNext)
            {
                escapeNext = false;
                continue;
            }

            if (ch == '\\')
            {
                escapeNext = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (ch == '{')
                {
                    braceCount++;
                }
                else if (ch == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        endIndex = i;
                        break;
                    }
                }
            }
        }

        if (braceCount != 0)
        {
            return string.Empty;
        }

        return json.Substring(startIndex, endIndex - startIndex + 1);
    }

    internal static List<string> ParseTopLevelKeys(string jsonObject)
    {
        var result = new List<string>();
        int depth = 0;
        bool inString = false;
        int i = 0;

        while (i < jsonObject.Length)
        {
            var c = jsonObject[i];

            if (c == '\\' && inString)
            {
                i += 2; // Skip escaped character
                continue;
            }

            if (c == '"')
            {
                if (!inString)
                {
                    // Start of string
                    inString = true;
                    i++;
                    int start = i;

                    // Read until closing quote
                    while (i < jsonObject.Length)
                    {
                        if (jsonObject[i] == '\\')
                        {
                            i += 2;
                            continue;
                        }

                        if (jsonObject[i] == '"')
                        {
                            break;
                        }

                        i++;
                    }

                    var stringValue = jsonObject.Substring(start, i - start);

                    // Check if this is a property name at depth 1 (top-level)
                    if (depth == 1)
                    {
                        int look = i + 1;
                        while (look < jsonObject.Length && char.IsWhiteSpace(jsonObject[look]))
                        {
                            look++;
                        }

                        if (look < jsonObject.Length && jsonObject[look] == ':')
                        {
                            result.Add(stringValue);
                        }
                    }

                    inString = false;
                }
            }
            else if (!inString)
            {
                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                }
            }

            i++;
        }

        return result;
    }

    internal static Dictionary<string, string[]> ParseAliasesFromJson(string aliasesJson)
    {
        var aliases = new Dictionary<string, string[]>();
        var inString = false;
        var escapeNext = false;
        var currentToken = new StringBuilder();
        var currentKey = string.Empty;
        var currentAliases = new List<string>();
        var inArray = false;
        var collectingKey = true;

        // Skip opening and closing braces
        for (int i = 1; i < aliasesJson.Length - 1; i++)
        {
            var ch = aliasesJson[i];

            if (escapeNext)
            {
                if (inString)
                {
                    currentToken.Append(ch);
                }

                escapeNext = false;
                continue;
            }

            if (ch == '\\')
            {
                if (inString)
                {
                    currentToken.Append(ch);
                }

                escapeNext = true;
                continue;
            }

            if (ch == '"')
            {
                if (!inString)
                {
                    // Start of string
                    inString = true;
                    currentToken.Clear();
                }
                else
                {
                    // End of string
                    inString = false;
                    var tokenValue = currentToken.ToString();

                    if (collectingKey)
                    {
                        currentKey = tokenValue;
                        collectingKey = false;
                    }
                    else if (inArray)
                    {
                        currentAliases.Add(tokenValue);
                    }
                }

                continue;
            }

            if (inString)
            {
                currentToken.Append(ch);
                continue;
            }

            // Handle structural characters outside of strings
            switch (ch)
            {
                case '[':
                    inArray = true;
                    currentAliases.Clear();
                    break;

                case ']':
                    inArray = false;
                    if (!string.IsNullOrEmpty(currentKey) && currentAliases.Count > 0)
                    {
                        aliases[currentKey] = currentAliases.ToArray();
                    }

                    break;

                case ',':
                    if (!inArray)
                    {
                        // End of key-value pair, reset for next key
                        collectingKey = true;
                        currentKey = string.Empty;
                        currentAliases.Clear();
                    }

                    break;
            }
        }

        return aliases;
    }
}
