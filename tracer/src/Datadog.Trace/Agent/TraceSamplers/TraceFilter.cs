// <copyright file="TraceFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.Agent.TraceSamplers;

/// <summary>
/// Evaluates trace-level filtering rules received from the agent's /info endpoint.
/// Filters are applied to the root span only, before stats computation.
/// </summary>
internal sealed class TraceFilter
{
    private readonly List<string> _filterTagsRequire;
    private readonly List<string> _filterTagsReject;
    private readonly List<RegexTagFilter> _filterTagsRegexRequire;
    private readonly List<RegexTagFilter> _filterTagsRegexReject;
    private readonly List<Regex> _ignoreResources;

    public TraceFilter(AgentTraceFilterConfig config)
    {
        _filterTagsRequire = config.FilterTagsRequire ?? [];
        _filterTagsReject = config.FilterTagsReject ?? [];
        _filterTagsRegexRequire = CompileTagFilters(config.FilterTagsRegexRequire);
        _filterTagsRegexReject = CompileTagFilters(config.FilterTagsRegexReject);
        _ignoreResources = CompilePatterns(config.IgnoreResources);
    }

    /// <summary>
    /// Returns true if the trace should be kept, false if it should be rejected.
    /// Evaluation is based on the root span only.
    /// </summary>
    public bool ShouldKeepTrace(Span rootSpan)
    {
        // 1. Resource filtering: reject if resource matches any ignore_resources pattern
        if (_ignoreResources.Count > 0 && !string.IsNullOrEmpty(rootSpan.ResourceName))
        {
            foreach (var pattern in _ignoreResources)
            {
                if (pattern.IsMatch(rootSpan.ResourceName))
                {
                    return false;
                }
            }
        }

        // 2. Reject filtering: reject if any tag matches reject filters
        foreach (var filter in _filterTagsReject)
        {
            if (MatchesExactFilter(rootSpan, filter))
            {
                return false;
            }
        }

        foreach (var filter in _filterTagsRegexReject)
        {
            if (MatchesRegexTagFilter(rootSpan, filter))
            {
                return false;
            }
        }

        // 3. Require filtering: ALL require filters must match
        foreach (var filter in _filterTagsRequire)
        {
            if (!MatchesExactFilter(rootSpan, filter))
            {
                return false;
            }
        }

        foreach (var filter in _filterTagsRegexRequire)
        {
            if (!MatchesRegexTagFilter(rootSpan, filter))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Matches an exact filter against span tags.
    /// Filter format: "key" (matches any tag with this key) or "key:value" (matches specific key-value).
    /// </summary>
    private static bool MatchesExactFilter(Span span, string filter)
    {
        var colonIndex = filter.IndexOf(':');
        if (colonIndex < 0)
        {
            // Key-only filter: matches if tag key exists with any value
            return span.GetTag(filter) is not null;
        }

        var key = filter.Substring(0, colonIndex);
        var value = filter.Substring(colonIndex + 1);
        return span.GetTag(key) == value;
    }

    /// <summary>
    /// Matches a regex tag filter against span tags.
    /// Per the spec, patterns are either "key_pattern" (matches any tag whose key matches)
    /// or "key_pattern:value_pattern" (both key and value must match their respective patterns).
    /// </summary>
    private static bool MatchesRegexTagFilter(Span span, RegexTagFilter filter)
    {
        var processor = new RegexTagFilterProcessor(filter);
        span.Tags.EnumerateTags(ref processor);
        return processor.Matched;
    }

    private static List<Regex> CompilePatterns(List<string>? patterns)
    {
        if (patterns is null or { Count: 0 })
        {
            return [];
        }

        var compiled = new List<Regex>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (!string.IsNullOrEmpty(pattern))
            {
                compiled.Add(new Regex(pattern, RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1)));
            }
        }

        return compiled;
    }

    /// <summary>
    /// Parses regex filter patterns into structured filters that match key and value separately.
    /// Format: "key_pattern" (key-only) or "key_pattern:value_pattern" (key and value).
    /// </summary>
    private static List<RegexTagFilter> CompileTagFilters(List<string>? patterns)
    {
        if (patterns is null or { Count: 0 })
        {
            return [];
        }

        var filters = new List<RegexTagFilter>(patterns.Count);
        foreach (var pattern in patterns)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                continue;
            }

            var colonIndex = pattern.IndexOf(':');
            if (colonIndex < 0)
            {
                // Key-only pattern: matches any tag whose key matches
                filters.Add(new RegexTagFilter(
                    new Regex(pattern, RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1)),
                    valuePattern: null));
            }
            else
            {
                // key_pattern:value_pattern — both must match
                var keyPart = pattern.Substring(0, colonIndex);
                var valuePart = pattern.Substring(colonIndex + 1);
                filters.Add(new RegexTagFilter(
                    new Regex(keyPart, RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1)),
                    new Regex(valuePart, RegexOptions.Compiled, matchTimeout: TimeSpan.FromSeconds(1))));
            }
        }

        return filters;
    }

    /// <summary>
    /// A parsed regex tag filter with separate key and optional value patterns.
    /// </summary>
    private readonly struct RegexTagFilter
    {
        public readonly Regex KeyPattern;
        public readonly Regex? ValuePattern;

        public RegexTagFilter(Regex keyPattern, Regex? valuePattern)
        {
            KeyPattern = keyPattern;
            ValuePattern = valuePattern;
        }
    }

    private struct RegexTagFilterProcessor : IItemProcessor<string>
    {
        private readonly RegexTagFilter _filter;
        public bool Matched;

        public RegexTagFilterProcessor(RegexTagFilter filter)
        {
            _filter = filter;
            Matched = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Process(TagItem<string> item)
        {
            if (!Matched && item.Value is not null)
            {
                if (_filter.KeyPattern.IsMatch(item.Key))
                {
                    // Key-only filter: any matching key is sufficient
                    // Key:Value filter: value must also match
                    Matched = _filter.ValuePattern is null || _filter.ValuePattern.IsMatch(item.Value);
                }
            }
        }
    }
}
