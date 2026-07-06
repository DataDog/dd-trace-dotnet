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

        // SHA256(targetingKey) -> set of serial ids.
        private readonly Dictionary<string, HashSet<long>> _subjects = new();

        // flagKey -> value string (first-wins).
        private readonly Dictionary<string, string> _defaults = new();

        internal static string HashTargetingKey(string targetingKey) => Sha256Helper.ComputeHashAsHexString(targetingKey);

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
            if (serialId is null && hasVariant)
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
        internal void AccumulateForRoot(IEvaluation? evaluation, string? targetingKey)
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
                valueStr = TruncateValue(valueStr, MaxDefaultValueLength);
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

        internal IReadOnlyList<KeyValuePair<string, string>> ToSpanTags()
        {
            var tags = new List<KeyValuePair<string, string>>(3);
            long[]? serialIds = null;
            Dictionary<string, long[]>? subjects = null;
            Dictionary<string, string>? defaults = null;

            lock (_gate)
            {
                if (_serialIds.Count > 0)
                {
                    serialIds = new long[_serialIds.Count];
                    _serialIds.CopyTo(serialIds);
                }

                if (_subjects.Count > 0)
                {
                    subjects = new Dictionary<string, long[]>(_subjects.Count);
                    foreach (var pair in _subjects)
                    {
                        var ids = new long[pair.Value.Count];
                        pair.Value.CopyTo(ids);
                        subjects[pair.Key] = ids;
                    }
                }

                if (_defaults.Count > 0)
                {
                    defaults = new Dictionary<string, string>(_defaults);
                }
            }

            if (serialIds is not null)
            {
                var enc = ULeb128Encoder.EncodeDeltaVarint(serialIds);
                if (!string.IsNullOrEmpty(enc))
                {
                    tags.Add(new KeyValuePair<string, string>(TagFlagsEnc, enc));
                }
            }

            if (subjects is not null)
            {
                var encoded = new Dictionary<string, string>(subjects.Count);
                foreach (var pair in subjects)
                {
                    encoded[pair.Key] = ULeb128Encoder.EncodeDeltaVarint(pair.Value);
                }

                tags.Add(new KeyValuePair<string, string>(TagSubjectsEnc, JsonHelper.SerializeObject(encoded)));
            }

            if (defaults is not null)
            {
                tags.Add(new KeyValuePair<string, string>(TagRuntimeDefaults, JsonHelper.SerializeObject(defaults)));
            }

            return tags;
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

        private static string TruncateValue(string value, int maxChars)
        {
            if (value.Length <= maxChars)
            {
                return value;
            }

            return value.Substring(0, maxChars);
        }
    }
}
