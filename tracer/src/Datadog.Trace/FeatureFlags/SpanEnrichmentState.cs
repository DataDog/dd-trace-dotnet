// <copyright file="SpanEnrichmentState.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Datadog.Trace.Logging;
using Datadog.Trace.Util.Json;

namespace Datadog.Trace.FeatureFlags
{
    /// <summary>
    /// Per-root-span accumulator for FFE APM feature-flag span enrichment. Ported verbatim from
    /// the frozen Node reference (dd-trace-js#8343): enforces the frozen limits, dedupes serial
    /// ids structurally (a <see cref="SortedSet{T}"/>), SHA256-hex hashes subject targeting keys,
    /// JSON-stringifies object runtime defaults (NOT ToString), and UTF-8-safe truncates default
    /// values to 64 chars. Created lazily only when the gate is on and a serial id / default is
    /// actually seen, so there is no idle per-span overhead when the gate is off.
    /// </summary>
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

        // Per-instance lock guarding every mutator AND the tag-production method. A single root
        // span can legitimately accumulate from multiple concurrent flag evaluations (e.g. a
        // Task.WhenAll fan-out of async resolves under one ambient root), and the same instance is
        // handed to each by SpanEnrichmentStore.GetOrAdd. The collections below are not
        // thread-safe; without this lock concurrent AddSerialId/AddSubject would corrupt the
        // red-black trees, and a straggler Add racing Span.Finish's drain would throw
        // "Collection was modified". The Node reference never had to solve this because the JS
        // event loop serializes per request; the .NET port must add the synchronization the
        // runtime requires.
        private readonly object _gate = new();

        // Dedupe is structural (a sorted set), matching the Node Set<number>.
        private readonly SortedSet<long> _serialIds = new();

        // SHA256(targetingKey) -> set of serial ids.
        private readonly Dictionary<string, SortedSet<long>> _subjects = new();

        // flagKey -> value string (first-wins).
        private readonly Dictionary<string, string> _defaults = new();

        public void AddSerialId(long id)
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

        public void AddSubject(string targetingKey, long id)
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

                _subjects[hashed] = new SortedSet<long> { id };
            }
        }

        public void AddDefault(string flagKey, object? value)
        {
            // Stringify outside the lock (pure, no shared state) to keep the critical section short.
            var valueStr = StringifyDefault(value);
            if (valueStr.Length > MaxDefaultValueLength)
            {
                valueStr = TruncateUtf8Safe(valueStr, MaxDefaultValueLength);
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

        // subjects are NOT checked: AddSubject never runs without AddSerialId.
        public bool HasData()
        {
            lock (_gate)
            {
                return _serialIds.Count > 0 || _defaults.Count > 0;
            }
        }

        /// <summary>
        /// Produces the contract-conformant <c>ffe_*</c> tags: <c>ffe_flags_enc</c> is a
        /// bare base64 string; <c>ffe_subjects_enc</c> and <c>ffe_runtime_defaults</c> are
        /// JSON-stringified objects. Empty components are omitted.
        /// </summary>
        /// <remarks>
        /// This MUST materialize the result inside <see cref="_gate"/> and return a fully-built list,
        /// NOT a deferred <c>yield</c> iterator: the caller (<c>Span.Finish()</c>) enumerates the
        /// result in a <c>foreach</c>, and a deferred iterator would run its body — reading the live
        /// <see cref="_serialIds"/>/<see cref="_subjects"/> collections — outside any lock, racing a
        /// concurrent <c>Add</c>. Building the list under the lock makes the snapshot atomic.
        /// </remarks>
        /// <returns>The tag key/value pairs to write on the root span.</returns>
        public IReadOnlyList<KeyValuePair<string, string>> ToSpanTags()
        {
            var tags = new List<KeyValuePair<string, string>>(3);

            lock (_gate)
            {
                if (_serialIds.Count > 0)
                {
                    var enc = ULeb128Encoder.EncodeDeltaVarint(_serialIds);
                    if (!string.IsNullOrEmpty(enc))
                    {
                        tags.Add(new KeyValuePair<string, string>(TagFlagsEnc, enc));
                    }
                }

                if (_subjects.Count > 0)
                {
                    var encoded = new Dictionary<string, string>(_subjects.Count);
                    foreach (var pair in _subjects)
                    {
                        encoded[pair.Key] = ULeb128Encoder.EncodeDeltaVarint(pair.Value);
                    }

                    tags.Add(new KeyValuePair<string, string>(TagSubjectsEnc, JsonHelper.SerializeObject(encoded)));
                }

                if (_defaults.Count > 0)
                {
                    tags.Add(new KeyValuePair<string, string>(TagRuntimeDefaults, JsonHelper.SerializeObject(_defaults)));
                }
            }

            return tags;
        }

        internal static string HashTargetingKey(string targetingKey)
        {
            // SHA256 -> lowercase hex digest (frozen contract). Use a manual hex loop so the
            // same code compiles across every supported TFM (Convert.ToHexStringLower is net9+).
#if NET6_0_OR_GREATER
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(Encoding.UTF8.GetBytes(targetingKey), hash);
#else
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(targetingKey));
#endif
            const string hexChars = "0123456789abcdef";
            var chars = new char[hash.Length * 2];
            for (var i = 0; i < hash.Length; i++)
            {
                var b = hash[i];
                chars[(i * 2)] = hexChars[b >> 4];
                chars[(i * 2) + 1] = hexChars[b & 0x0F];
            }

            return new string(chars);
        }

        // Object default -> JSON (matches Node's JSON.stringify); scalars -> their string form.
        // A bare string is emitted as-is (Node's String(value) for a string is the string itself).
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
                    // Arrays / dictionaries / complex objects -> JSON (NOT ToString()).
                    return JsonHelper.SerializeObject(value);
            }
        }

        // UTF-8-safe truncation: never split a surrogate pair when slicing to maxChars.
        private static string TruncateUtf8Safe(string value, int maxChars)
        {
            if (value.Length <= maxChars)
            {
                return value;
            }

            var end = maxChars;
            if (char.IsHighSurrogate(value[end - 1]))
            {
                end--;
            }

            return value.Substring(0, end);
        }
    }
}
