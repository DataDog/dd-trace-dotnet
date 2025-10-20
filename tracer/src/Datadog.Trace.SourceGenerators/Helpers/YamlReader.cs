// <copyright file="YamlReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.SourceGenerators.Helpers
{
    /// <summary>
    /// Simple YAML parser for reading documentation strings from YAML files.
    /// Supports basic YAML features needed for configuration documentation.
    /// </summary>
    internal static class YamlReader
    {
        /// <summary>
        /// Parses a YAML file containing configuration key documentation.
        /// Expects format: KEY: | followed by multi-line documentation
        /// </summary>
        public static Dictionary<string, string> ParseDocumentation(string yamlContent)
        {
            var result = new Dictionary<string, string>();
            var lines = yamlContent.Replace("\r\n", "\n").Split('\n');

            string? currentKey = null;
            var currentDoc = new StringBuilder();
            var inMultiLine = false;
            var baseIndent = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // Skip empty lines and comments when not in multi-line
                if (!inMultiLine && (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")))
                {
                    continue;
                }

                // Check for new key (starts at column 0, contains colon)
                if (!inMultiLine && line.Length > 0 && line[0] != ' ' && line.Contains(":"))
                {
                    // Save previous key if exists
                    if (currentKey != null)
                    {
                        result[currentKey] = currentDoc.ToString().TrimEnd();
                        currentDoc.Clear();
                    }

                    var colonIndex = line.IndexOf(':');
                    currentKey = line.Substring(0, colonIndex).Trim();

                    // Check if it's a multi-line string (|)
                    var afterColon = line.Substring(colonIndex + 1).Trim();
                    if (afterColon == "|" || afterColon == ">")
                    {
                        inMultiLine = true;
                        baseIndent = -1; // Will be set on first content line
                    }
                    else if (!string.IsNullOrEmpty(afterColon))
                    {
                        // Single line value
                        currentDoc.Append(afterColon);
                        result[currentKey] = currentDoc.ToString();
                        currentDoc.Clear();
                        currentKey = null;
                    }

                    continue;
                }

                // Handle multi-line content
                if (inMultiLine && currentKey != null)
                {
                    // Check if we've reached the next key (no indentation)
                    if (line.Length > 0 && line[0] != ' ' && line.Contains(":"))
                    {
                        // Save current key and process this line as new key
                        result[currentKey] = currentDoc.ToString().TrimEnd();
                        currentDoc.Clear();
                        inMultiLine = false;

                        var colonIndex = line.IndexOf(':');
                        currentKey = line.Substring(0, colonIndex).Trim();
                        var afterColon = line.Substring(colonIndex + 1).Trim();
                        if (afterColon == "|" || afterColon == ">")
                        {
                            inMultiLine = true;
                            baseIndent = -1;
                        }

                        continue;
                    }

                    // Determine base indentation from first content line
                    if (baseIndent == -1 && line.Length > 0 && line[0] == ' ')
                    {
                        baseIndent = 0;
                        while (baseIndent < line.Length && line[baseIndent] == ' ')
                        {
                            baseIndent++;
                        }
                    }

                    // Add content line (remove base indentation)
                    if (line.Length > 0)
                    {
                        var contentStart = 0;
                        while (contentStart < line.Length && contentStart < baseIndent && line[contentStart] == ' ')
                        {
                            contentStart++;
                        }

                        if (currentDoc.Length > 0)
                        {
                            currentDoc.AppendLine();
                        }

                        currentDoc.Append(line.Substring(contentStart));
                    }
                    else
                    {
                        // Empty line in multi-line content
                        if (currentDoc.Length > 0)
                        {
                            currentDoc.AppendLine();
                        }
                    }
                }
            }

            // Save last key
            if (currentKey != null)
            {
                result[currentKey] = currentDoc.ToString().TrimEnd();
            }

            return result;
        }
    }
}
