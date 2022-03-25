// <copyright file="CodeOwners.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;

namespace Datadog.Trace.Ci
{
    internal class CodeOwners
    {
        private readonly Entry[] _entries;

        public CodeOwners(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            var entriesList = new List<Entry>();
            var lines = File.ReadAllLines(filePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line) || line[0] == '#')
                {
                    continue;
                }

                var finalLine = line;
                var ownersList = new List<string>();
                var terms = line.Split(new[] { ' ' }, StringSplitOptions.None);
                for (var i = 0; i < terms.Length; i++)
                {
                    var currentTerm = terms[i];
                    if (currentTerm.Length == 0)
                    {
                        continue;
                    }

                    // Teams and users handles starts with @
                    // Emails contains @
                    if (currentTerm[0] == '@' || currentTerm.Contains("@"))
                    {
                        ownersList.Add(currentTerm);
                        finalLine = finalLine.Replace(currentTerm, string.Empty);
                    }
                }

                finalLine = finalLine.Trim();
                if (finalLine.Length == 0)
                {
                    continue;
                }

                entriesList.Add(new Entry(finalLine, ownersList.ToArray()));
            }

            entriesList.Reverse();
            _entries = entriesList.ToArray();
        }

        public Entry? Match(string value)
        {
            foreach (var entry in _entries)
            {
                var pattern = entry.Pattern;
                var finalPattern = pattern;

                bool includeAnythingBefore;
                bool includeAnythingAfter;

                if (pattern.StartsWith("/"))
                {
                    includeAnythingBefore = false;
                }
                else
                {
                    if (finalPattern.StartsWith("*"))
                    {
                        finalPattern = finalPattern.Substring(1);
                    }

                    includeAnythingBefore = true;
                }

                if (pattern.EndsWith("/"))
                {
                    includeAnythingAfter = true;
                }
                else if (pattern.EndsWith("/*"))
                {
                    includeAnythingAfter = true;
                    finalPattern = finalPattern.Substring(0, finalPattern.Length - 1);
                }
                else
                {
                    includeAnythingAfter = false;
                }

                if (includeAnythingAfter)
                {
                    var found = includeAnythingBefore ? value.Contains(finalPattern) : value.StartsWith(finalPattern);
                    if (!found)
                    {
                        continue;
                    }

                    if (!pattern.EndsWith("/*"))
                    {
                        return entry;
                    }

                    var patternEnd = value.IndexOf(finalPattern, StringComparison.Ordinal);
                    if (patternEnd != -1)
                    {
                        patternEnd += finalPattern.Length;
                        var remainingString = value.Substring(patternEnd);
                        if (remainingString.IndexOf("/", StringComparison.Ordinal) == -1)
                        {
                            return entry;
                        }
                    }
                }
                else
                {
                    if (includeAnythingBefore)
                    {
                        if (value.EndsWith(finalPattern))
                        {
                            return entry;
                        }
                    }
                    else if (value == finalPattern)
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        internal readonly struct Entry
        {
            public readonly string Pattern;
            public readonly string[] Owners;

            public Entry(string pattern, string[] owners)
            {
                Pattern = pattern;
                Owners = owners;
            }

            public string GetOwnersString()
            {
                return "[\"" + string.Join("\",\"", Owners) + "\"]";
            }
        }
    }
}
