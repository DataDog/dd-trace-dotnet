// <copyright file="TraceFilter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
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
    private readonly List<Regex> _filterTagsRegexRequire;
    private readonly List<Regex> _filterTagsRegexReject;
    private readonly List<Regex> _ignoreResources;

    public TraceFilter(AgentTraceFilterConfig config)
    {
        _filterTagsRequire = config.FilterTagsRequire ?? [];
        _filterTagsReject = config.FilterTagsReject ?? [];
        _filterTagsRegexRequire = CompilePatterns(config.FilterTagsRegexRequire);
        _filterTagsRegexReject = CompilePatterns(config.FilterTagsRegexReject);
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

        foreach (var pattern in _filterTagsRegexReject)
        {
            if (MatchesRegexFilter(rootSpan, pattern))
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

        foreach (var pattern in _filterTagsRegexRequire)
        {
            if (!MatchesRegexFilter(rootSpan, pattern))
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
    /// Matches a regex filter against span tags.
    /// The regex is matched against the "key:value" string of each tag.
    /// </summary>
    private static bool MatchesRegexFilter(Span span, Regex pattern)
    {
        var processor = new RegexTagMatchProcessor(pattern);
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

    private struct RegexTagMatchProcessor : IItemProcessor<string>
    {
        private readonly Regex _pattern;
        public bool Matched;

        public RegexTagMatchProcessor(Regex pattern)
        {
            _pattern = pattern;
            Matched = false;
        }

        public void Process(TagItem<string> item)
        {
            if (!Matched && item.Value is not null)
            {
                Matched = _pattern.IsMatch($"{item.Key}:{item.Value}");
            }
        }
    }
}
