// <copyright file="FlagEvaluationAggregator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Datadog.Trace.FeatureFlags.FlagEvaluation;

/// <summary>
/// Two-tier aggregation (full → degraded → drop-counted). Thread-safe via a single lock.
/// </summary>
internal sealed class FlagEvaluationAggregator
{
    // Context pruning limits — mirror worker.ts MAX_EVALUATION_CONTEXT_FIELDS / MAX_FIELD_LENGTH.
    internal const int MaxContextFields = 256;
    internal const int MaxFieldLength = 256;

    // Type discriminator bytes — one per .NET type so int 1 and string "1" encode differently.
    private const byte TagString = (byte)'s';
    private const byte TagBool = (byte)'b';
    private const byte TagInt = (byte)'i';
    private const byte TagLong = (byte)'l';
    private const byte TagDouble = (byte)'f';
    private const byte TagOther = (byte)'o';

    private readonly object _lock = new();
    private readonly int _globalCap;
    private readonly int _perFlagCap;
    private readonly int _degradedCap;

    private Dictionary<FullKey, EvaluationEntry> _full;
    private Dictionary<DegradedKey, EvaluationEntry> _degraded;
    private Dictionary<string, int> _perFlagFull; // flagKey → count of full-tier attempts
    private int _globalCount;
    private long _droppedDegradedOverflow;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlagEvaluationAggregator"/> class.
    /// </summary>
    public FlagEvaluationAggregator(int globalCap, int perFlagCap, int degradedCap)
    {
        _globalCap = globalCap;
        _perFlagCap = perFlagCap;
        _degradedCap = degradedCap;
        _full = new Dictionary<FullKey, EvaluationEntry>();
        _degraded = new Dictionary<DegradedKey, EvaluationEntry>();
        _perFlagFull = new Dictionary<string, int>();
    }

#pragma warning disable SA1204 // static helpers follow instance methods for readability

    /// <summary>
    /// Applies 256-field / 256-char limits. Keeps the first MaxContextFields non-oversized entries
    /// in alphabetical key order (deterministic cut so identical contexts always prune identically).
    /// </summary>
    internal static Dictionary<string, object?> PruneContext(Dictionary<string, object?> attrs)
    {
        if (attrs.Count == 0)
        {
            return attrs;
        }

        // Check if pruning is necessary.
        bool needsPrune = attrs.Count > MaxContextFields;
        if (!needsPrune)
        {
            foreach (object? v in attrs.Values)
            {
                if (v is string s && s.Length > MaxFieldLength)
                {
                    needsPrune = true;
                    break;
                }
            }
        }

        if (!needsPrune)
        {
            return attrs;
        }

        // Deterministic prune: sort keys first, then take first MaxContextFields non-oversized.
        var keys = new List<string>(attrs.Keys);
        keys.Sort(StringComparer.Ordinal);

        var result = new Dictionary<string, object?>(Math.Min(attrs.Count, MaxContextFields));
        int count = 0;
        foreach (string k in keys)
        {
            if (count >= MaxContextFields)
            {
                break;
            }

            object? v = attrs[k];
            if (v is string sv && sv.Length > MaxFieldLength)
            {
                // Skip oversized string values.
                continue;
            }

            result[k] = v;
            count++;
        }

        return result;
    }

    /// <summary>
    /// Builds the EXACT, comparable canonical-context key for the pruned context map.
    /// Encoding: sorted keys, each field = length-delimited-key + type-tag + length-delimited-value.
    /// No hash — distinct contexts always produce distinct keys (no digest, so no collisions).
    /// Exposed internal for unit testing.
    /// </summary>
    internal static string CanonicalContextKey(Dictionary<string, object?>? attrs)
    {
        if (attrs is null || attrs.Count == 0)
        {
            return string.Empty;
        }

        var keys = new List<string>(attrs.Keys);
        keys.Sort(StringComparer.Ordinal);

        // Build encoding into a byte list, then convert once to string using Latin1 (1:1 byte→char).
        var buf = new List<byte>(256);
        foreach (string k in keys)
        {
            AppendLengthDelimited(buf, Encoding.UTF8.GetBytes(k));
            AppendContextValue(buf, attrs[k]);
        }

        // Use GetEncoding("iso-8859-1") for netstandard2.0/net461 compat (Latin1 property not available).
        return Encoding.GetEncoding("iso-8859-1").GetString(buf.ToArray());
    }

    /// <summary>
    /// Records one evaluation into the appropriate aggregation tier.
    /// Implements the two-tier cascade: full → degraded → drop(counted).
    /// </summary>
    public void Add(FlagEvalEvent ev)
    {
        bool runtimeDefault = ev.Variant is null;
        string variant = ev.Variant ?? string.Empty;
        long evalTimeMs = ev.EvalTimeMs;

        // Prune context (256 fields / 256 chars) before building the key.
        Dictionary<string, object?>? contextAttrs = ev.ContextAttrs is { Count: > 0 }
            ? PruneContext(ev.ContextAttrs)
            : null;

        string contextKey = CanonicalContextKey(contextAttrs);

        var fullKey = new FullKey(
            ev.FlagKey,
            variant,
            ev.AllocationKey,
            ev.Reason,
            ev.TargetingKey,
            contextKey);

        lock (_lock)
        {
            // Fast path: existing full-tier bucket → increment only.
            if (_full.TryGetValue(fullKey, out EvaluationEntry? existing))
            {
                existing.Observe(evalTimeMs);
                return;
            }

            // Check per-flag cap.
            _perFlagFull.TryGetValue(ev.FlagKey, out int perFlagCount);
            if (perFlagCount >= _perFlagCap)
            {
                AddToDegraded(ev.FlagKey, variant, ev.AllocationKey, ev.Reason, evalTimeMs, runtimeDefault);
                return;
            }

            // Increment per-flag attempt counter regardless of whether we can create the full bucket
            // (so the per-flag overflow path stays active even when globalCap is full).
            _perFlagFull[ev.FlagKey] = perFlagCount + 1;

            // Check global cap before creating a new full-tier bucket.
            if (_globalCount >= _globalCap)
            {
                AddToDegraded(ev.FlagKey, variant, ev.AllocationKey, ev.Reason, evalTimeMs, runtimeDefault);
                return;
            }

            // New full-tier entry.
            _full[fullKey] = new EvaluationEntry(evalTimeMs, runtimeDefault, contextAttrs);
            _globalCount++;
        }
    }

    /// <summary>
    /// Drains and resets the aggregator. Thread-safe: swaps out the maps under the lock.
    /// </summary>
    public DrainResult Drain()
    {
        Dictionary<FullKey, EvaluationEntry> full;
        Dictionary<DegradedKey, EvaluationEntry> degraded;
        long dropped;

        lock (_lock)
        {
            full = _full;
            degraded = _degraded;
            dropped = _droppedDegradedOverflow;

            _full = new Dictionary<FullKey, EvaluationEntry>();
            _degraded = new Dictionary<DegradedKey, EvaluationEntry>();
            _perFlagFull = new Dictionary<string, int>();
            _globalCount = 0;
            _droppedDegradedOverflow = 0;
        }

        return new DrainResult(full, degraded, dropped);
    }

    // Called with _lock held.
    private void AddToDegraded(string flagKey, string variant, string allocationKey, string reason, long evalTimeMs, bool runtimeDefault)
    {
        var degKey = new DegradedKey(flagKey, variant, allocationKey, reason);
        if (_degraded.TryGetValue(degKey, out EvaluationEntry? existing))
        {
            existing.Observe(evalTimeMs);
            return;
        }

        // New degraded bucket — check cap.
        if (_degraded.Count >= _degradedCap)
        {
            // Terminal tier: degraded cap reached — drop the count but keep it observable.
            _droppedDegradedOverflow++;
            return;
        }

        _degraded[degKey] = new EvaluationEntry(evalTimeMs, runtimeDefault, contextAttrs: null);
    }

    private static void AppendLengthDelimited(List<byte> buf, byte[] b)
    {
        // 8-byte big-endian length prefix so '='/'\n'-bearing values cannot fake a field boundary.
        ulong len = (ulong)b.Length;
        buf.Add((byte)(len >> 56));
        buf.Add((byte)(len >> 48));
        buf.Add((byte)(len >> 40));
        buf.Add((byte)(len >> 32));
        buf.Add((byte)(len >> 24));
        buf.Add((byte)(len >> 16));
        buf.Add((byte)(len >> 8));
        buf.Add((byte)len);
        buf.AddRange(b);
    }

    private static void AppendContextValue(List<byte> buf, object? v)
    {
        byte tag;
        byte[] payload;

        switch (v)
        {
            case string s:
                tag = TagString;
                payload = Encoding.UTF8.GetBytes(s);
                break;
            case bool b:
                tag = TagBool;
                payload = Encoding.UTF8.GetBytes(b ? "true" : "false");
                break;
            case int i:
                tag = TagInt;
                payload = Encoding.UTF8.GetBytes(i.ToString());
                break;
            case long l:
                tag = TagLong;
                payload = Encoding.UTF8.GetBytes(l.ToString());
                break;
            case double d:
                tag = TagDouble;
                payload = Encoding.UTF8.GetBytes(d.ToString("G"));
                break;
            default:
                tag = TagOther;
                payload = Encoding.UTF8.GetBytes(v?.ToString() ?? string.Empty);
                break;
        }

        buf.Add(tag);
        AppendLengthDelimited(buf, payload);
    }
}
