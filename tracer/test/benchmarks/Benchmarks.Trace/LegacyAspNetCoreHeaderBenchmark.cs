#if NETFRAMEWORK

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Headers;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.Propagators;
using Datadog.Trace.Util;

namespace Benchmarks.Trace;

[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class LegacyAspNetCoreHeaderBenchmark
{
    private LegacyAspNetCoreHeadersCollectionAdapter _headers;

    [Params("missing", "single", "multiple")]
    public string HeaderName { get; set; } = null!;

    [GlobalSetup]
    public void Setup()
    {
        var store = new HeaderStore
        {
            ["single"] = new LegacyStringValues("42"),
            ["multiple"] = new LegacyStringValues(["invalid", "42"]),
        };
        _headers = new LegacyAspNetCoreHeadersCollectionAdapter(store.DuckCast<ILegacyAspNetCoreHeaders>());
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ParseString")]
    public string? ParseStringEnumerable() => ParseStringUsingEnumerable(_headers, HeaderName);

    [Benchmark]
    [BenchmarkCategory("ParseString")]
    public string? ParseStringList() => ParseUtility.ParseString(_headers, HeaderName);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ParseUInt64")]
    public ulong? ParseUInt64Enumerable()
        => ParseUInt64UsingEnumerable(_headers, _headers.GetAccessor(), HeaderName);

    [Benchmark]
    [BenchmarkCategory("ParseUInt64")]
    public ulong? ParseUInt64List()
        => ParseUtility.ParseUInt64(_headers, _headers.GetAccessor(), HeaderName);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("GetHeaderValue")]
    public string? GetHeaderValueEnumerable() => GetHeaderValueUsingEnumerable(_headers, HeaderName);

    [Benchmark]
    [BenchmarkCategory("GetHeaderValue")]
    public string? GetHeaderValueList()
        => LegacyAspNetCoreHttpRequestHandler.GetHeaderValue(_headers, HeaderName);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? ParseStringUsingEnumerable(LegacyAspNetCoreHeadersCollectionAdapter headers, string name)
    {
        var values = headers.GetValues(name);
        if (values is string[] array)
        {
            foreach (var value in array)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }

        return ParseStringEnumerable(values);

        static string? ParseStringEnumerable(IEnumerable<string> enumerable)
        {
            foreach (var value in enumerable)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ulong? ParseUInt64UsingEnumerable<TCarrier, TCarrierGetter>(TCarrier carrier, TCarrierGetter getter, string name)
        where TCarrierGetter : struct, ICarrierGetter<TCarrier>
    {
        var values = getter.Get(carrier, name);
        if (values is string[] array)
        {
            foreach (var value in array)
            {
                if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }
            }

            return null;
        }

        return ParseUInt64Enumerable(values);

        static ulong? ParseUInt64Enumerable(IEnumerable<string?> enumerable)
        {
            foreach (var value in enumerable)
            {
                if (ulong.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
                {
                    return result;
                }
            }

            return null;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string? GetHeaderValueUsingEnumerable(LegacyAspNetCoreHeadersCollectionAdapter headers, string name)
    {
        using var enumerator = headers.GetValues(name).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var first = enumerator.Current;
        if (!enumerator.MoveNext())
        {
            return first;
        }

        var builder = StringBuilderCache.Acquire();
        builder.Append(first);
        do
        {
            builder.Append(',');
            builder.Append(enumerator.Current);
        }
        while (enumerator.MoveNext());

        return StringBuilderCache.GetStringAndRelease(builder);
    }

    private sealed class HeaderStore
    {
        private readonly Dictionary<string, LegacyStringValues> _values = new(StringComparer.OrdinalIgnoreCase);

        public LegacyStringValues this[string name]
        {
            get
            {
                _values.TryGetValue(name, out var value);
                return value;
            }

            set => _values[name] = value;
        }
    }

    // Matches the two-reference layout and collection interfaces used by StringValues 2.1 and 2.2.
    private readonly struct LegacyStringValues : IList<string>, IReadOnlyList<string>
    {
        private readonly string? _value;
        private readonly string[]? _values;

        public LegacyStringValues(string value)
        {
            _value = value;
            _values = null;
        }

        public LegacyStringValues(string[] values)
        {
            _value = null;
            _values = values;
        }

        public int Count => _values?.Length ?? (_value is null ? 0 : 1);

        bool ICollection<string>.IsReadOnly => true;

        string IList<string>.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        public string this[int index] => _values is not null ? _values[index] : index == 0 && _value is not null ? _value : throw new ArgumentOutOfRangeException(nameof(index));

        int IList<string>.IndexOf(string item) => throw new NotSupportedException();

        bool ICollection<string>.Contains(string item) => throw new NotSupportedException();

        void ICollection<string>.CopyTo(string[] array, int arrayIndex) => throw new NotSupportedException();

        void ICollection<string>.Add(string item) => throw new NotSupportedException();

        void IList<string>.Insert(int index, string item) => throw new NotSupportedException();

        bool ICollection<string>.Remove(string item) => throw new NotSupportedException();

        void IList<string>.RemoveAt(int index) => throw new NotSupportedException();

        void ICollection<string>.Clear() => throw new NotSupportedException();

        public Enumerator GetEnumerator() => new(this);

        IEnumerator<string> IEnumerable<string>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<string>
        {
            private readonly LegacyStringValues _values;
            private int _index;

            public Enumerator(LegacyStringValues values)
            {
                _values = values;
                _index = -1;
            }

            public string Current => _values[_index];

            object IEnumerator.Current => Current;

            public bool MoveNext() => ++_index < _values.Count;

            public void Reset() => _index = -1;

            public void Dispose()
            {
            }
        }
    }
}

#endif
