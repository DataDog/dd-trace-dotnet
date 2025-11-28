// <copyright file="DiagnosticFeatureResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ExtensionMethods;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

internal sealed class DiagnosticFeatureResult
{
    private DiagnosticFeatureResult(IReadOnlyList<string> loaded, IReadOnlyList<string> failed, IReadOnlyList<string> skipped, IReadOnlyDictionary<string, object> errors, IReadOnlyDictionary<string, object> warnings)
    {
        Loaded = loaded;
        Failed = failed;
        Skipped = skipped;
        Errors = errors;
        Warnings = warnings;
    }

    public IReadOnlyList<string> Loaded { get; }

    public IReadOnlyList<string> Failed { get; }

    public IReadOnlyList<string> Skipped { get; }

    public IReadOnlyDictionary<string, object> Errors { get; }

    public IReadOnlyDictionary<string, object> Warnings { get; }

    public static DiagnosticFeatureResult? From(string key, Dictionary<string, object?> diagObject)
    {
        if (diagObject.TryGetValue(key, out var subKeysObj) && subKeysObj is Dictionary<string, object> subKeys)
        {
            var loaded = GetListKey(subKeys, "loaded");
            var failed = GetListKey(subKeys, "failed");
            var skipped = GetListKey(subKeys, "skipped");
            var errors = GetDictionaryKey(subKeys, "errors");
            var warnings = GetDictionaryKey(subKeys, "warnings");
            return new DiagnosticFeatureResult(loaded, failed, skipped, errors, warnings);
        }

        return null;
    }

    private static IReadOnlyList<string> GetListKey(Dictionary<string, object> diagObject, string key)
    {
        if (diagObject.TryGetValue(key, out var listObj) && listObj is List<object> list)
        {
            return list.Cast<string>().ToList();
        }

        return new List<string>();
    }

    private static IReadOnlyDictionary<string, object> GetDictionaryKey(Dictionary<string, object> diagObject, string key)
    {
        if (diagObject.TryGetValue(key, out var dictObj) && dictObj is Dictionary<string, object> dict)
        {
            return dict;
        }

        return new Dictionary<string, object>();
    }
}
