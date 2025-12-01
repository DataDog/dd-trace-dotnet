// <copyright file="ProcessTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Configuration;
using Datadog.Trace.Processors;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace;

internal static class ProcessTags
{
    public const string EntrypointName = "entrypoint.name";
    public const string EntrypointBasedir = "entrypoint.basedir";
    public const string EntrypointWorkdir = "entrypoint.workdir";

    // two views on the same data
    public static readonly List<string> TagsList = GetTagsList();
    public static readonly string SerializedTags = GetSerializedTagsFromList(TagsList);

    private static List<string> GetTagsList()
    {
        // ⚠️ make sure entries are added in alphabetical order of keys
        var tags = new List<string>(3); // Update if you add more entries below
        tags.AddNormalizedTag(EntrypointBasedir, GetLastPathSegment(AppContext.BaseDirectory));
        tags.AddNormalizedTag(EntrypointName, GetEntryPointName());
        // workdir can be changed by the code, but we consider that capturing the value when this is called is good enough
        tags.AddNormalizedTag(EntrypointWorkdir, GetLastPathSegment(Environment.CurrentDirectory));

        return tags;
    }

    /// <summary>
    /// normalizes the tag value (keys are hardcoded so they don't need that)
    /// and adds it to the list iff not null or empty
    /// </summary>
    private static void AddNormalizedTag(this List<string> tags, string key, string? value)
    {
        if (StringUtil.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalizedValue = NormalizeTagValue(value);
        // check length because normalization can squish the string to nothing
        if (normalizedValue.Length > 0)
        {
            tags.Add($"{key}:{normalizedValue}");
        }
    }

    [TestingAndPrivateOnly]
    internal static string NormalizeTagValue(string tagValue)
    {
        // TraceUtil.NormalizeTag does almost exactly what we want, except it allows ':', which we don't want because we use it as a key/value separator.
        // We need to replace ':' before calling NormalizeTag because there is a logic to remove duplicate underscores.
        var normalized = TraceUtil.NormalizeTag(tagValue.Replace(oldChar: ':', newChar: '_'));

        // truncate to 100 char, which the max allowed for a service name, and this is the only usage for those tags
        if (normalized.Length > 100)
        {
            return normalized.Substring(startIndex: 0, length: 100);
        }

        return normalized;
    }

    private static string GetSerializedTagsFromList(List<string> tags)
    {
        return string.Join(",", tags);
    }

    /// <summary>
    /// From the full path of a directory, get the name of the leaf directory.
    /// </summary>
    private static string GetLastPathSegment(string directoryPath)
    {
        // Path.GetFileName returns an empty string if the path ends with a '/'.
        // We could use Path.TrimEndingDirectorySeparator instead of the trim here, but it's not available on .NET Framework
        return Path.GetFileName(directoryPath.TrimEnd('\\').TrimEnd('/'));
    }

    private static string? GetEntryPointName()
    {
        return EntryAssemblyLocator.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
    }
}
