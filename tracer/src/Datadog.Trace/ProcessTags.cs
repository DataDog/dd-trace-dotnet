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

internal sealed class ProcessTags
{
    public const string EntrypointName = "entrypoint.name";
    public const string EntrypointBasedir = "entrypoint.basedir";
    public const string EntrypointWorkdir = "entrypoint.workdir";
    public const string ServiceSetByUser = "svc.user";
    public const string ServiceAuto = "svc.auto";

    public ProcessTags(bool serviceNameUserDefined, string autoServiceName)
    {
        // Build the complete tags list with svc.user and svc.auto
        TagsList = GetTagsList(serviceNameUserDefined, autoServiceName);

        // Build the serialized version with svc.user and svc.auto
        SerializedTags = GetSerializedTagsFromList(TagsList);
    }

    // two views on the same data
    public List<string> TagsList
    {
        get;
    }

    public string SerializedTags
    {
        get;
    }

    private static List<string> GetTagsList(bool serviceNameUserDefined, string autoServiceName)
    {
        // ⚠️ make sure entries are added in alphabetical order of keys
        var tags = new List<string>(4); // Update if you add more entries below
        AddNormalizedTag(tags, EntrypointBasedir, GetLastPathSegment(AppContext.BaseDirectory));
        AddNormalizedTag(tags, EntrypointName, GetEntryPointName());
        // workdir can be changed by the code, but we consider that capturing the value when this is called is good enough
        AddNormalizedTag(tags, EntrypointWorkdir, GetLastPathSegment(Environment.CurrentDirectory));
        // Either svc.user or svc.auto, never both
        if (serviceNameUserDefined)
        {
            // svc.user is only added when the user explicitly set the service name
            tags.Add($"{ServiceSetByUser}:1");
        }
        else
        {
            // svc.auto contains the automatically determined service name when user didn't set it
            AddNormalizedTag(tags, ServiceAuto, autoServiceName);
        }

        return tags;
    }

    private static string GetSerializedTagsFromList(IEnumerable<string> tags)
    {
        return string.Join(",", tags);
    }

    /// <summary>
    /// normalizes the tag value (keys are hardcoded so they don't need that)
    /// and adds it to the list iff not null or empty
    /// </summary>
    private static void AddNormalizedTag(List<string> tags, string key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        // TraceUtil.NormalizeTag does almost exactly what we want, except it allows ':',
        // which we don't want because we use it as a key/value separator.
        var normalizedValue = TraceUtil.NormalizeTag(value).Replace(oldChar: ':', newChar: '_');
        tags.Add($"{key}:{normalizedValue}");
    }

    /// <summary>
    /// From the full path of a directory, get the name of the leaf directory.
    /// </summary>
    [TestingAndPrivateOnly]
    internal static string GetLastPathSegment(string directoryPath)
    {
        // See https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats
        // Use DirectoryInfo.Name because it correctly handles
        // - "/" and "\" separators
        // - paths with or without trailing separators
        // - root paths like "/" or "C:\"
        // - other edge cases on Windows like device paths ("\\?\.\" etc) or "\\server\share" UNC paths
        return StringUtil.IsNullOrEmpty(directoryPath) ?
                   string.Empty :
                   new DirectoryInfo(directoryPath).Name;
    }

    private static string? GetEntryPointName()
    {
        return EntryAssemblyLocator.GetEntryAssembly()?.EntryPoint?.DeclaringType?.FullName;
    }
}
