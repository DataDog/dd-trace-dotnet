// <copyright file="EventGridMemoizingEnumerable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Replaces an arbitrary <see cref="IEnumerable{T}"/> argument with a replayable wrapper without
/// taking a compile-time dependency on the enumerable's item type.
/// </summary>
internal static class EventGridMemoizingEnumerable
{
    private interface IFactory
    {
        object Create(object events, IEventGridMemoizingEnumerableObserver observer);
    }

    internal static bool TryWrap<TEvents>(ref TEvents events, IEventGridMemoizingEnumerableObserver observer)
    {
        if ((object?)events is null || FactoryCache<TEvents>.Factory is not { } factory)
        {
            return false;
        }

        events = (TEvents)factory.Create(events, observer);
        return true;
    }

    private static Type? GetEnumerableType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>))
        {
            return type;
        }

        foreach (var interfaceType in type.GetInterfaces())
        {
            if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                return interfaceType;
            }
        }

        return null;
    }

    private static class FactoryCache<TEvents>
    {
        public static readonly IFactory? Factory = CreateFactory();

        private static IFactory? CreateFactory()
        {
            var eventsType = typeof(TEvents);
            if (GetEnumerableType(eventsType) is not { } enumerableType)
            {
                return null;
            }

            var eventType = enumerableType.GetGenericArguments()[0];
            var wrapperType = typeof(MemoizingEnumerable<>).MakeGenericType(eventType);
            if (!eventsType.IsAssignableFrom(wrapperType))
            {
                return null;
            }

            var factoryType = typeof(Factory<>).MakeGenericType(eventType);
            return Activator.CreateInstance(factoryType, nonPublic: true) as IFactory;
        }
    }

    private sealed class Factory<TEvent> : IFactory
    {
        public object Create(object events, IEventGridMemoizingEnumerableObserver observer)
            => new MemoizingEnumerable<TEvent>((IEnumerable<TEvent>)events, observer);
    }

    private sealed class MemoizingEnumerable<TEvent> : IEnumerable<TEvent>
    {
        private readonly object _lock = new();
        private readonly IEnumerable<TEvent> _source;
        private readonly IEventGridMemoizingEnumerableObserver _observer;
        private readonly List<TEvent> _items = [];
        private IEnumerator<TEvent>? _sourceEnumerator;
        private ExceptionDispatchInfo? _exception;
        private bool _completed;

        public MemoizingEnumerable(IEnumerable<TEvent> source, IEventGridMemoizingEnumerableObserver observer)
        {
            _source = source;
            _observer = observer;
        }

        public IEnumerator<TEvent> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private bool TryGetItem(int index, out TEvent item)
        {
            lock (_lock)
            {
                if (index < _items.Count)
                {
                    item = _items[index];
                    return true;
                }

                if (_exception is { } exception)
                {
                    exception.Throw();
                }

                if (_completed)
                {
                    item = default!;
                    return false;
                }

                try
                {
                    _sourceEnumerator ??= _source.GetEnumerator();
                    if (!_sourceEnumerator.MoveNext())
                    {
                        _sourceEnumerator.Dispose();
                        _sourceEnumerator = null;
                        _completed = true;
                        NotifyCompleted();
                        item = default!;
                        return false;
                    }

                    item = _sourceEnumerator.Current;
                    _items.Add(item);
                    NotifyItem(item);
                    return true;
                }
                catch (Exception ex)
                {
                    _exception = ExceptionDispatchInfo.Capture(ex);
                    _completed = true;

                    try
                    {
                        _sourceEnumerator?.Dispose();
                    }
                    catch
                    {
                        // Preserve the original enumeration exception.
                    }

                    _sourceEnumerator = null;
                    throw;
                }
            }
        }

        private void NotifyItem(TEvent item)
        {
            try
            {
                _observer.OnItem(item);
            }
            catch
            {
                // Instrumentation must not affect customer enumeration.
            }
        }

        private void NotifyCompleted()
        {
            try
            {
                _observer.OnCompleted(_items.Count);
            }
            catch
            {
                // Instrumentation must not affect customer enumeration.
            }
        }

        private sealed class Enumerator : IEnumerator<TEvent>
        {
            private readonly MemoizingEnumerable<TEvent> _owner;
            private int _index;
            private TEvent _current = default!;

            public Enumerator(MemoizingEnumerable<TEvent> owner)
            {
                _owner = owner;
            }

            public TEvent Current => _current;

            object? IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (!_owner.TryGetItem(_index, out _current))
                {
                    return false;
                }

                _index++;
                return true;
            }

            public void Reset() => throw new NotSupportedException();

            // The source enumerator is shared so that another consumer can continue from the cached prefix.
            // It is disposed when the source completes or throws.
            public void Dispose()
            {
            }
        }
    }
}
