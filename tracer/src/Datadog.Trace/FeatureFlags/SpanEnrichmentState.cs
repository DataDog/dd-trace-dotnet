// <copyright file="SpanEnrichmentState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.FeatureFlags
{
    internal sealed class SpanEnrichmentState
    {
        internal const int MaxSerialIds = 200;
        internal const int MaxSubjects = 10;
        internal const int MaxExperimentsPerSubject = 20;
        internal const int MaxDefaults = 5;
        internal const int MaxDefaultValueLength = 64;

        // Bare base64 string.
        internal const string TagFlagsEnc = "ffe_flags_enc";

        // JSON object string: { sha256hex: base64, ... }.
        internal const string TagSubjectsEnc = "ffe_subjects_enc";

        // JSON object string: { flagKey: valueStr, ... }.
        internal const string TagRuntimeDefaults = "ffe_runtime_defaults";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SpanEnrichmentState));

        // Guards the bounded state against concurrent flag evaluations on the same trace (e.g. a
        // Task.WhenAll fan-out). Tag production snapshots under this lock, then encodes and
        // serializes after releasing it.
        private readonly object _gate = new();

        private readonly HashSet<long> _serialIds = new();

        // SHA256-hex(targeting key) -> set of serial ids. The raw targeting key (often a customer
        // identifier, and unbounded in length) is hashed at accumulation time and never retained on
        // the trace, so we don't pin user-controlled data for the trace/writer-queue lifetime.
        private readonly Dictionary<string, HashSet<long>> _subjects = new();

        // flagKey -> value string (first-wins).
        private readonly Dictionary<string, string> _defaults = new();

        /// <summary>
        /// Gets or sets a fault-injection hook invoked at the start of <see cref="BuildSpanTags"/>. Test-only,
        /// used to verify the serializer-thread write path never lets enrichment break span serialization.
        /// </summary>
        internal Action? OnBuildSpanTagsForTesting { get; set; }

        internal static string HashTargetingKey(string targetingKey) => Sha256Helper.ComputeHashAsHexString(targetingKey);

        /// <summary>
        /// Returns whether an evaluation would record anything: a serial id, or a runtime default
        /// (no variant). A variant with no serial id records nothing, so callers can use this to
        /// skip creating per-trace state for evaluations that would be dropped anyway.
        /// </summary>
        internal static bool IsRecordable(long? serialId, bool hasVariant) => serialId is not null || !hasVariant;

        /// <summary>
        /// Returns whether a native evaluation would record anything, without allocating state.
        /// </summary>
        internal static bool IsRecordable(IEvaluation? evaluation)
        {
            if (evaluation is null)
            {
                return false;
            }

            // A runtime default (no variant) always records; a variant only records when it carries
            // a serial id.
            if (StringUtil.IsNullOrEmpty(evaluation.Variant))
            {
                return true;
            }

            var metadata = evaluation.FlagMetadata;
            return metadata is not null
                && metadata.TryGetValue(FeatureFlagMetadataKeys.SplitSerialId, out var serialId)
                && !StringUtil.IsNullOrEmpty(serialId);
        }

        /// <summary>
        /// Accumulates a single flag evaluation into this trace's state. Never throws.
        /// </summary>
        /// <param name="serialId">The split serial id, or null when absent.</param>
        /// <param name="doLog">Whether the allocation authorizes subject logging.</param>
        /// <param name="targetingKey">The evaluation context targeting key, or null.</param>
        /// <param name="hasVariant">Whether the evaluation produced a (non-empty) variant.</param>
        /// <param name="flagKey">The flag key (used for runtime defaults).</param>
        /// <param name="value">The evaluated value (used for runtime defaults).</param>
        internal void Accumulate(long? serialId, bool doLog, string? targetingKey, bool hasVariant, string flagKey, object? value)
        {
            // A variant without a serial id is a plain evaluation with nothing to record.
            if (!IsRecordable(serialId, hasVariant))
            {
                return;
            }

            try
            {
                if (serialId.HasValue)
                {
                    AddSerialId(serialId.Value);
                    if (doLog && !StringUtil.IsNullOrEmpty(targetingKey))
                    {
                        AddSubject(targetingKey!, serialId.Value);
                    }
                }
                else if (!hasVariant)
                {
                    AddDefault(flagKey, value);
                }
            }
            catch (Exception ex)
            {
                // Enrichment must never break flag evaluation.
                Log.Debug(ex, "SpanEnrichmentState.Accumulate failed");
            }
        }

        /// <summary>
        /// Accumulates a native FeatureFlags SDK evaluation into this trace's state. Never throws.
        /// </summary>
        /// <param name="evaluation">The completed evaluation returned by the evaluator.</param>
        /// <param name="targetingKey">The caller's targeting key, or null.</param>
        internal void AccumulateEvaluation(IEvaluation? evaluation, string? targetingKey)
        {
            if (evaluation is null)
            {
                return;
            }

            long? serialId = null;
            var metadata = evaluation.FlagMetadata;
            if (metadata is not null &&
                metadata.TryGetValue(FeatureFlagMetadataKeys.SplitSerialId, out var serialIdStr) &&
                !StringUtil.IsNullOrEmpty(serialIdStr) &&
                long.TryParse(serialIdStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                serialId = parsed;
            }

            var doLog =
                metadata is not null &&
                metadata.TryGetValue(FeatureFlagMetadataKeys.DoLog, out var doLogStr) &&
                string.Equals(doLogStr, "true", StringComparison.OrdinalIgnoreCase);

            Accumulate(
                serialId,
                doLog,
                targetingKey,
                hasVariant: !StringUtil.IsNullOrEmpty(evaluation.Variant),
                evaluation.FlagKey,
                evaluation.Value);
        }

        internal void AddSerialId(long id)
        {
            lock (_gate)
            {
                if (_serialIds.Count >= MaxSerialIds && !_serialIds.Contains(id))
                {
                    Log.Debug<int>("SpanEnrichmentState: serial id limit ({Max}) reached, dropping id", MaxSerialIds);
                    return;
                }

                _serialIds.Add(id);
            }
        }

        internal void AddSubject(string targetingKey, long id)
        {
            // Hash the raw targeting key immediately so only the fixed-size digest is retained on the
            // trace (never the raw, potentially user-identifying value). Hashing here (evaluation path)
            // rather than in BuildSpanTags() keeps customer-controlled data off the trace/writer queue.
            var hashed = HashTargetingKey(targetingKey);
            lock (_gate)
            {
                if (_subjects.TryGetValue(hashed, out var ids))
                {
                    if (ids.Count >= MaxExperimentsPerSubject && !ids.Contains(id))
                    {
                        Log.Debug<int>("SpanEnrichmentState: experiments-per-subject limit ({Max}) reached, dropping id", MaxExperimentsPerSubject);
                        return;
                    }

                    ids.Add(id);
                    return;
                }

                if (_subjects.Count >= MaxSubjects)
                {
                    Log.Debug<int>("SpanEnrichmentState: subject limit ({Max}) reached, dropping subject", MaxSubjects);
                    return;
                }

                _subjects[hashed] = [id];
            }
        }

        internal void AddDefault(string flagKey, object? value)
        {
            var valueStr = StringifyDefault(value);
            if (valueStr.Length > MaxDefaultValueLength)
            {
                valueStr = valueStr.Substring(0, MaxDefaultValueLength);
            }

            lock (_gate)
            {
                // First-wins: do not overwrite an existing flag default.
                if (_defaults.ContainsKey(flagKey))
                {
                    return;
                }

                if (_defaults.Count >= MaxDefaults)
                {
                    Log.Debug<int>("SpanEnrichmentState: runtime-default limit ({Max}) reached, dropping default", MaxDefaults);
                    return;
                }

                _defaults[flagKey] = valueStr;
            }
        }

        internal bool HasData()
        {
            lock (_gate)
            {
                return _serialIds.Count > 0 || _defaults.Count > 0;
            }
        }

        /// <summary>
        /// Builds the encoded <c>ffe_*</c> tag values for the root span. Runs on the serializer
        /// thread (from <c>SpanMessagePackFormatter</c>), so encoding, JSON serialization and
        /// subject hashing stay off the customer's <c>Span.Finish()</c> path. Never throws; on
        /// failure it returns <see langword="default"/> so serialization is never broken.
        /// </summary>
        internal FeatureFlagSpanTags BuildSpanTags()
        {
            try
            {
                OnBuildSpanTagsForTesting?.Invoke();

                long[]? serialIds = null;
                Dictionary<string, long[]>? subjects = null;
                Dictionary<string, string>? defaults = null;

                lock (_gate)
                {
                    if (_serialIds.Count > 0)
                    {
                        serialIds = [.. _serialIds];
                    }

                    if (_subjects.Count > 0)
                    {
                        subjects = new Dictionary<string, long[]>(_subjects.Count);
                        foreach (var pair in _subjects)
                        {
                            subjects[pair.Key] = [.. pair.Value];
                        }
                    }

                    if (_defaults.Count > 0)
                    {
                        defaults = new Dictionary<string, string>(_defaults);
                    }
                }

                string? flagsEnc = null;
                if (serialIds is not null)
                {
                    var enc = ULeb128Encoder.EncodeDeltaVarint(serialIds);
                    if (!StringUtil.IsNullOrEmpty(enc))
                    {
                        flagsEnc = enc;
                    }
                }

                string? subjectsEnc = null;
                if (subjects is not null)
                {
                    // Keys are already SHA256-hex digests (hashed at accumulation time in AddSubject).
                    var encoded = new Dictionary<string, string>(subjects.Count);
                    foreach (var pair in subjects)
                    {
                        encoded[pair.Key] = ULeb128Encoder.EncodeDeltaVarint(pair.Value);
                    }

                    subjectsEnc = JsonHelper.SerializeObject(encoded);
                }

                string? runtimeDefaults = defaults is not null ? JsonHelper.SerializeObject(defaults) : null;

                return new FeatureFlagSpanTags(flagsEnc, subjectsEnc, runtimeDefaults);
            }
            catch (Exception ex)
            {
                // Enrichment must never break span serialization.
                Log.Debug(ex, "SpanEnrichmentState.BuildSpanTags failed");
                return default;
            }
        }

        // Object default -> JSON; scalars -> their string form. A bare string is emitted as-is.
        private static string StringifyDefault(object? value)
        {
            switch (value)
            {
                case null:
                    return "null";
                case string s:
                    return s;
                case bool b:
                    return b ? "true" : "false";
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                case float or double or decimal:
                    return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                default:
                    return JsonHelper.SerializeObject(value);
            }
        }
    }
}
