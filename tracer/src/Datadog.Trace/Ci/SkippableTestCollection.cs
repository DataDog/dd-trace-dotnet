// <copyright file="SkippableTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci;

/// <summary>
/// An immutable response-owned parameter index, with an allocation-free view of a local module's candidates.
/// </summary>
internal readonly struct SkippableTestCollection : IReadOnlyList<SkippableTest>
{
    private readonly ParameterIndex? _index;
    private readonly string? _moduleName;
    private readonly bool _filterByModule;

    public SkippableTestCollection(IList<SkippableTest> candidates)
    {
        _index = candidates.Count == 0 ? null : new ParameterIndex(candidates);
        _moduleName = null;
        _filterByModule = false;
    }

    private SkippableTestCollection(ParameterIndex? index, string? moduleName)
    {
        _index = index;
        _moduleName = moduleName;
        _filterByModule = true;
    }

    // Keep module views O(1) to create: matching does not need the filtered count.
    [TestingOnly]
    public int Count
    {
        get
        {
            if (_index is null)
            {
                return 0;
            }

            if (!_filterByModule)
            {
                return _index.Candidates.Length;
            }

            var count = 0;
            foreach (var candidate in _index.Candidates)
            {
                if (candidate.MatchesModuleScope(_moduleName))
                {
                    count++;
                }
            }

            return count;
        }
    }

    [TestingOnly]
    public SkippableTest this[int index]
    {
        get
        {
            if (_index is not null && index >= 0)
            {
                foreach (var candidate in _index.Candidates)
                {
                    if ((!_filterByModule || candidate.MatchesModuleScope(_moduleName)) && index-- == 0)
                    {
                        return candidate;
                    }
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public Enumerator GetEnumerator() => new(this);

    IEnumerator<SkippableTest> IEnumerable<SkippableTest>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    internal SkippableTestCollection ForModule(string? moduleName) => new(_index, moduleName);

    internal bool TryMatch(ref Query query, out SkippableTest candidate)
    {
        if (_index is not null)
        {
            return _index.TryMatch(ref query, _filterByModule, _moduleName, out candidate);
        }

        candidate = default;
        return false;
    }

    private static int HashArgument(string name, string value)
        => unchecked((StringComparer.Ordinal.GetHashCode(name) * 397) ^ StringComparer.Ordinal.GetHashCode(value));

    public struct Enumerator : IEnumerator<SkippableTest>
    {
        private readonly SkippableTestCollection _collection;
        private int _position;

        internal Enumerator(SkippableTestCollection collection)
        {
            _collection = collection;
            _position = -1;
            Current = default;
        }

        public SkippableTest Current { readonly get; private set; }

        readonly object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_collection._index is { } index)
            {
                while (_position < index.Candidates.Length - 1)
                {
                    var candidate = index.Candidates[++_position];
                    if (!_collection._filterByModule || candidate.MatchesModuleScope(_collection._moduleName))
                    {
                        Current = candidate;
                        return true;
                    }
                }
            }

            Current = default;
            return false;
        }

        readonly void IEnumerator.Reset() => throw new NotSupportedException();

        public readonly void Dispose()
        {
        }
    }

    /// <summary>
    /// Formats local arguments at most once, even when both the method name and display name are queried.
    /// Each synchronous lookup owns its query; pass it by ref rather than copying its rented buffer ownership.
    /// Dispose after the final lookup to clear and return the buffer. Only the immutable index is shared between threads.
    /// </summary>
    internal ref struct Query
    {
        private readonly object[]? _arguments;
        private readonly ParameterInfo[]? _parameters;
        private readonly string? _metadataTestName;
        private readonly bool _includeMetadataTestNameInFingerprint;
        private readonly bool _useParameterIndexForUnnamedParameters;
        private string[]? _values;
        private string? _fingerprint;
        private int _argumentsHash;
        private bool? _hasDuplicateParameterNames;

        public Query(object[]? arguments, ParameterInfo[]? parameters, string? metadataTestName, bool includeMetadataTestNameInFingerprint, bool useParameterIndexForUnnamedParameters)
        {
            _arguments = arguments;
            _parameters = parameters;
            _metadataTestName = metadataTestName;
            _includeMetadataTestNameInFingerprint = includeMetadataTestNameInFingerprint;
            _useParameterIndexForUnnamedParameters = useParameterIndexForUnnamedParameters;
            _values = null;
            _fingerprint = null;
            _argumentsHash = 0;
            _hasDuplicateParameterNames = null;
        }

        public readonly bool HasNoArguments => _arguments is null || _arguments.Length == 0;

        public readonly bool CanMatchArguments => _arguments is not null && _parameters is not null;

        public readonly int ParameterCount => _parameters?.Length ?? 0;

        public readonly string? MetadataTestName => StringUtil.IsNullOrEmpty(_metadataTestName) ? null : _metadataTestName;

        public bool HasDuplicateParameterNames()
        {
            if (_hasDuplicateParameterNames is { } hasDuplicates)
            {
                return hasDuplicates;
            }

            // This compares the method's parameters, not the historical candidates. Cache across name lookups.
            if (_parameters is { } parameters)
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    for (var j = 0; j < i; j++)
                    {
                        if ((parameters[i].Name ?? string.Empty) == (parameters[j].Name ?? string.Empty))
                        {
                            _hasDuplicateParameterNames = true;
                            return true;
                        }
                    }
                }
            }

            _hasDuplicateParameterNames = false;
            return false;
        }

        public int GetArgumentsHash()
        {
            EnsureValues();
            return _argumentsHash;
        }

        public string GetFingerprint()
        {
            if (_fingerprint is null)
            {
                EnsureValues();
                _fingerprint = Common.CreateTestParameters(
                    new ReadOnlySpan<object?>(_values, 0, ParameterCount),
                    _parameters,
                    _includeMetadataTestNameInFingerprint ? _metadataTestName : null,
                    _useParameterIndexForUnnamedParameters).GetFingerprint();
            }

            return _fingerprint;
        }

        public bool Matches(TestParameters? parameters)
        {
            if (parameters?.TryGetFingerprint(out var expectedFingerprint) == true)
            {
                return string.Equals(expectedFingerprint, GetFingerprint(), StringComparison.Ordinal);
            }

            if ((parameters?.Arguments is null || parameters.Arguments.Count == 0) && HasNoArguments)
            {
                return MetadataMatches(parameters);
            }

            if (parameters?.Arguments is not { } arguments || _arguments is null || _parameters is not { } methodParameters || arguments.Count != methodParameters.Length)
            {
                return false;
            }

            EnsureValues();
            for (var i = 0; i < ParameterCount; i++)
            {
                if (!arguments.TryGetValue(methodParameters[i].Name ?? string.Empty, out var value) ||
                    (value as string ?? value?.ToString() ?? "(null)") != _values[i])
                {
                    return false;
                }
            }

            return MetadataMatches(parameters);
        }

        public void Dispose()
        {
            if (_values is { Length: > 0 })
            {
                ArrayPool<string>.Shared.Return(_values, clearArray: true);
            }

            _values = null;
        }

        private readonly bool MetadataMatches(TestParameters? parameters)
        {
            if (parameters?.Metadata is null || !parameters.Metadata.TryGetValue(TestTags.MetadataTestName, out var expected))
            {
                return true;
            }

            var expectedName = expected?.ToString();
            return StringUtil.IsNullOrEmpty(expectedName) ||
                   (!StringUtil.IsNullOrEmpty(_metadataTestName) && string.Equals(expectedName, _metadataTestName, StringComparison.Ordinal));
        }

        [MemberNotNull(nameof(_values))]
        private void EnsureValues()
        {
            if (_values is not null)
            {
                return;
            }

            if (_parameters is not { Length: > 0 } parameters)
            {
                _values = [];
                return;
            }

            _values = ArrayPool<string>.Shared.Rent(parameters.Length);
            _argumentsHash = parameters.Length;
            for (var i = 0; i < parameters.Length; i++)
            {
                var value = _arguments is not null && i < _arguments.Length ? Common.GetParametersValueData(_arguments[i]) : "(default)";
                _values[i] = value;
                _argumentsHash = unchecked(_argumentsHash + HashArgument(parameters[i].Name ?? string.Empty, value));
            }
        }
    }

    private readonly record struct MatchKey(int ArgumentsHash, string? Metadata, bool Fingerprint);

    private readonly struct Entry(TestParameters? parameters, int next)
    {
        public readonly TestParameters? Parameters = parameters;
        public readonly int Next = next;
    }

    private sealed class ParameterIndex
    {
        private readonly Dictionary<MatchKey, int> _heads;
        private readonly HashSet<int> _argumentCounts = [];
        private readonly Entry[] _entries;
        private readonly int _unindexedHead = -1;
        private readonly bool _hasFingerprints;

        public ParameterIndex(IList<SkippableTest> candidates)
        {
            Candidates = new SkippableTest[candidates.Count];
            _heads = new Dictionary<MatchKey, int>(candidates.Count);
            // Array-backed buckets avoid a separate List and backing array for every distinct parameter set.
            _entries = new Entry[candidates.Count];
            // Prepend in reverse response order so collisions and equivalent identities still select the first candidate.
            for (var i = candidates.Count - 1; i >= 0; i--)
            {
                var candidate = candidates[i];
                Candidates[i] = candidate;
                TestParameters? parameters = null;
                if (!StringUtil.IsNullOrWhiteSpace(candidate.RawParameters) && !candidate.TryGetParameters(out parameters))
                {
                    _entries[i] = new Entry(parameters: null, -2);
                    continue;
                }

                if (parameters?.TryGetFingerprint(out var fingerprint) == true)
                {
                    _hasFingerprints = true;
                    Add(new MatchKey(0, fingerprint, Fingerprint: true), parameters, i);
                }
                else if (TryGetKey(parameters, out var key))
                {
                    _argumentCounts.Add(parameters?.Arguments?.Count ?? 0);
                    Add(key, parameters, i);
                }
                else
                {
                    // Non-string JSON values use the calling thread's culture in the existing matching contract.
                    // Keep those uncommon candidates parsed, but compare their values at query time.
                    _entries[i] = new Entry(parameters, _unindexedHead);
                    _unindexedHead = i;
                }
            }
        }

        public SkippableTest[] Candidates { get; }

        public bool TryMatch(ref Query query, bool filterByModule, string? moduleName, out SkippableTest candidate)
        {
            var first = Candidates.Length;
            if (query.CanMatchArguments && query.HasDuplicateParameterNames())
            {
                // Unusual reflection metadata can repeat parameter names. Preserve the old dictionary lookup semantics.
                for (var i = 0; i < first; i++)
                {
                    if (_entries[i].Next != -2 && Matches(i, ref query, filterByModule, moduleName))
                    {
                        first = i;
                    }
                }
            }
            else
            {
                if (query.HasNoArguments)
                {
                    FindArguments(0, ref query, filterByModule, moduleName, ref first);
                }

                if (first != 0 && query.CanMatchArguments && _argumentCounts.Contains(query.ParameterCount))
                {
                    var hash = query.GetArgumentsHash();
                    if (hash != 0 || !query.HasNoArguments)
                    {
                        FindArguments(hash, ref query, filterByModule, moduleName, ref first);
                    }
                }

                if (first != 0 && _hasFingerprints)
                {
                    Find(new MatchKey(0, query.GetFingerprint(), Fingerprint: true), ref query, filterByModule, moduleName, ref first);
                }

                for (var i = _unindexedHead; i >= 0 && i < first; i = _entries[i].Next)
                {
                    if (Matches(i, ref query, filterByModule, moduleName))
                    {
                        first = i;
                    }
                }
            }

            candidate = first < Candidates.Length ? Candidates[first] : default;
            return first < Candidates.Length;
        }

        private static bool TryGetKey(TestParameters? parameters, out MatchKey key)
        {
            key = default;
            object? metadata = null;
            parameters?.Metadata?.TryGetValue(TestTags.MetadataTestName, out metadata);
            if (metadata is not null and not string)
            {
                return false;
            }

            var hash = parameters?.Arguments?.Count ?? 0;
            if (parameters?.Arguments is { } arguments)
            {
                foreach (var argument in arguments)
                {
                    if (argument.Value is not null and not string)
                    {
                        return false;
                    }

                    hash = unchecked(hash + HashArgument(argument.Key, (string?)argument.Value ?? "(null)"));
                }
            }

            var name = (string?)metadata;
            key = new MatchKey(hash, StringUtil.IsNullOrEmpty(name) ? null : name, Fingerprint: false);
            return true;
        }

        private void Add(MatchKey key, TestParameters? parameters, int index)
        {
            _entries[index] = new Entry(parameters, _heads.TryGetValue(key, out var next) ? next : -1);
            _heads[key] = index;
        }

        private void FindArguments(int hash, ref Query query, bool filterByModule, string? moduleName, ref int first)
        {
            Find(new MatchKey(hash, Metadata: null, Fingerprint: false), ref query, filterByModule, moduleName, ref first);
            if (first != 0 && query.MetadataTestName is { } metadata)
            {
                Find(new MatchKey(hash, metadata, Fingerprint: false), ref query, filterByModule, moduleName, ref first);
            }
        }

        private void Find(MatchKey key, ref Query query, bool filterByModule, string? moduleName, ref int first)
        {
            if (_heads.TryGetValue(key, out var index))
            {
                for (var i = index; i >= 0 && i < first; i = _entries[i].Next)
                {
                    if (Matches(i, ref query, filterByModule, moduleName))
                    {
                        first = i;
                    }
                }
            }
        }

        private bool Matches(int index, ref Query query, bool filterByModule, string? moduleName)
            => (!filterByModule || Candidates[index].MatchesModuleScope(moduleName)) && query.Matches(_entries[index].Parameters);
    }
}
