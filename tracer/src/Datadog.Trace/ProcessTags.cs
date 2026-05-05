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
    public const string ServiceSetByUser = "svc.user:true";
    public const string ServiceAuto = "svc.auto";

    private readonly bool _serviceNameUserDefined;
    private readonly string _autoServiceName;

    // ProcessTags captures EntryAssemblyLocator.GetEntryAssembly() and Environment.CurrentDirectory
    // If initialization happens before the entry assembly is available (common in ASP.NET/IIS or some test hosts),
    // entrypoint.name/entrypoint.workdir will be empty
    // We intentionally do not compute tags at construction and init them on first access only
    public ProcessTags(bool serviceNameUserDefined, string autoServiceName)
    {
        _serviceNameUserDefined = serviceNameUserDefined;
        _autoServiceName = autoServiceName;
    }

    // two views on the same data
    public List<string> TagsList => field ??= GetTagsList(_serviceNameUserDefined, _autoServiceName);

    public string SerializedTags
    {
        get => field ??= string.Join(",", TagsList); // don't forget to refresh the hash in ServiceRemappingHash on write if this value becomes mutable
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
        // svc.user is only added when the user explicitly set the service name
        // svc.auto contains the automatically determined service name when user didn't set it
        if (serviceNameUserDefined)
        {
            tags.Add(ServiceSetByUser);
        }
        else
        {
            AddNormalizedTag(tags, ServiceAuto, autoServiceName);
        }

        return tags;
    }

    /// <summary>
    /// normalizes the tag value (keys are hardcoded so they don't need that)
    /// and adds it to the list iff not null or empty
    /// </summary>
    private static void AddNormalizedTag(List<string> tags, string key, string? value)
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
