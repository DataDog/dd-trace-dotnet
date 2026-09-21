// <copyright file="CiTestSelectionParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Serilog;

namespace CiTestSelection;

internal class CiTestSelectionParser
{
    private List<Entry> _entriesList = new List<Entry>();

    public CiTestSelectionParser(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Log.Error($"Current Directory: {Environment.CurrentDirectory}");
            Log.Error(Path.GetFullPath(filePath));
            throw new ArgumentException("The CI test selection rules file path is invalid.", filePath);
        }

        _entriesList = File.ReadLines(filePath)
            .Where(line => !string.IsNullOrWhiteSpace(line) && line[0] != '#')
            .Select(ParseLine)
            .Reverse()
            .ToList();
    }

    private static Entry ParseLine(string line)
    {
        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var pattern = parts.First();
        var components = parts.Skip(1).ToArray();
        return new Entry(pattern, components);
    }

    private static bool IsFileIncluded(string fileName, string pattern)
    {
        string regexPattern = pattern.Replace("*", @"[^/\r\n\\]*");

        if (!pattern.StartsWith("/"))
        {
            regexPattern = ".*" + regexPattern;
        }

        if (!pattern.EndsWith("/"))
        {
            regexPattern += "$";
        }

        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        var match = regex.Match(fileName);
        return match.Success && match.Index == 0;
    }

    public Entry? Match(string fileName)
    {
        foreach (var entry in _entriesList)
        {
            if (IsFileIncluded(fileName, entry.Pattern))
            {
                return entry; // the last matching pattern (first in the entryList) takes the most precedence.
            }
        }

        return null;
    }

    internal readonly struct Entry
    {
        public readonly string Pattern;
        public readonly string[] Components;

        public Entry(string pattern, string[] components)
        {
            Pattern = pattern;
            Components = components ?? Array.Empty<string>();
        }
    }
}
