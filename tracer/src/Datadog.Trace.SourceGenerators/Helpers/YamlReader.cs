// <copyright file="YamlReader.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
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

            string? currentKey = null;
            var currentDoc = new StringBuilder();
            var inMultiLine = false;
            var baseIndent = 0;

            foreach (var line in new LineEnumerator(yamlContent.AsSpan()))
            {
                // Skip empty lines and comments when not in multi-line
                if (!inMultiLine && (IsWhiteSpace(line) || line.TrimStart().StartsWith("#".AsSpan(), StringComparison.Ordinal)))
                {
                    continue;
                }

                // Check for new key (starts at column 0, contains colon)
                if (!inMultiLine && line.Length > 0 && line[0] != ' ' && line.IndexOf(':') >= 0)
                {
                    // Save previous key if exists
                    if (currentKey != null)
                    {
                        result[currentKey] = currentDoc.ToString().TrimEnd();
                        currentDoc.Clear();
                    }

                    var colonIndex = line.IndexOf(':');
                    currentKey = line.Slice(0, colonIndex).Trim().ToString();

                    // Check if it's a multi-line string (|)
                    var afterColon = line.Slice(colonIndex + 1).Trim();
                    if (afterColon.Length == 1 && (afterColon[0] == '|' || afterColon[0] == '>'))
                    {
                        inMultiLine = true;
                        baseIndent = -1; // Will be set on first content line
                    }
                    else if (afterColon.Length > 0)
                    {
                        // Single line value
                        currentDoc.Append(afterColon.ToString());
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
                    if (line.Length > 0 && line[0] != ' ' && line.IndexOf(':') >= 0)
                    {
                        // Save current key and process this line as new key
                        result[currentKey] = currentDoc.ToString().TrimEnd();
                        currentDoc.Clear();
                        inMultiLine = false;

                        var colonIndex = line.IndexOf(':');
                        currentKey = line.Slice(0, colonIndex).Trim().ToString();
                        var afterColon = line.Slice(colonIndex + 1).Trim();
                        if (afterColon.Length == 1 && (afterColon[0] == '|' || afterColon[0] == '>'))
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

                        currentDoc.Append(line.Slice(contentStart).ToString());
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

        /// <summary>
        /// Checks if a span contains only whitespace characters.
        /// </summary>
        private static bool IsWhiteSpace(ReadOnlySpan<char> span)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (!char.IsWhiteSpace(span[i]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Enumerator for iterating through lines in a ReadOnlySpan without allocations.
        /// </summary>
        private ref struct LineEnumerator
        {
            private ReadOnlySpan<char> _remaining;
            private ReadOnlySpan<char> _current;
            private bool _isEnumeratorActive;

            public LineEnumerator(ReadOnlySpan<char> text)
            {
                _remaining = text;
                _current = default;
                _isEnumeratorActive = true;
            }

            public ReadOnlySpan<char> Current => _current;

            public LineEnumerator GetEnumerator() => this;

            public bool MoveNext()
            {
                if (!_isEnumeratorActive)
                {
                    return false;
                }

                if (_remaining.Length == 0)
                {
                    _isEnumeratorActive = false;
                    return false;
                }

                var idx = _remaining.IndexOfAny('\r', '\n');
                if (idx < 0)
                {
                    // Last line without line ending
                    _current = _remaining;
                    _remaining = default;
                    return true;
                }

                _current = _remaining.Slice(0, idx);

                // Skip past the line ending (\r\n or \n or \r)
                var advance = idx + 1;
                if (idx < _remaining.Length - 1 && _remaining[idx] == '\r' && _remaining[idx + 1] == '\n')
                {
                    advance = idx + 2;
                }

                _remaining = _remaining.Slice(advance);
                return true;
            }
        }
    }
}
