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

        // Per-instance lock guarding mutation and tag production. A single root span can accumulate
        // from concurrent flag evaluations under the same trace.
        private readonly object _gate = new();

        private readonly HashSet<long> _serialIds = new();

        // SHA256(targetingKey) -> set of serial ids.
        private readonly Dictionary<string, HashSet<long>> _subjects = new();

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

                _subjects[hashed] = [id];
            }
        }

        public void AddDefault(string flagKey, object? value)
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

        public bool HasData()
        {
            lock (_gate)
            {
                return _serialIds.Count > 0 || _defaults.Count > 0;
            }
        }

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
#if NET6_0_OR_GREATER
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(Encoding.UTF8.GetBytes(targetingKey), hash);
            return HexString.ToHexString(hash);
#else
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(targetingKey));
            return HexString.ToHexString(hash);
#endif
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
