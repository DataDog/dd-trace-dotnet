using System;
using System.Buffers;

#nullable enable

namespace Datadog.Trace.Agent.Events;

internal sealed class StringCache : IDisposable
{
    private const int DefaultInitialCapacity = 4;

    private readonly ArrayPool<string> _pool;

    private string[] _array;
    private int _count;

    public StringCache(ArrayPool<string>? pool = null, int initialCapacity = 0)
    {
        _pool = pool ?? ArrayPool<string>.Shared;
        _array = initialCapacity > 0 ? _pool.Rent(initialCapacity) : Array.Empty<string>();
    }

    public int Count => _count;

    public int Capacity => _array.Length;

    public Span<string> GetStrings() => _array.AsSpan(0, _count);

    public int TryAdd(string item)
    {
        for (var index = 0; index < _array.Length && index < _count; index++)
        {
            string s = _array[index];

            if (string.Equals(s, item, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return Add(item);
    }

    private int Add(string item)
    {
        GrowIfNeeded();
        _array[_count] = item;
        return _count++;
    }

    public void Clear()
    {
        Array.Clear(_array, 0, _array.Length);
        _count = 0;
    }

    public string this[int index]
    {
        get => _array[index];
        set => _array[index] = value;
    }

    private void GrowIfNeeded()
    {
        if (_count < _array.Length)
        {
            return;
        }

        if (_array.Length == 0)
        {
            _array = _pool.Rent(DefaultInitialCapacity);
            return;
        }

        var oldBuffer = _array;
        var newBuffer = _pool.Rent(_array.Length * 2);

        Array.Copy(oldBuffer, newBuffer, _count);
        _array = newBuffer;

        if (oldBuffer.Length > 0)
        {
            _pool.Return(oldBuffer);
        }
    }

    public void Dispose()
    {
        var buffer = _array;
        _array = Array.Empty<string>();

        if (buffer.Length > 0)
        {
            _pool.Return(buffer);
        }
    }
}
