// <copyright file="EventGridObservingEnumerable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Replaces an arbitrary <see cref="IEnumerable{T}"/> argument with a transparent observing wrapper without
/// taking a compile-time dependency on the enumerable's item type.
/// </summary>
/// <remarks>
/// Event Grid send APIs accept deferred or one-shot sequences. Enumerating them in the CallTarget begin callback
/// could execute customer code early, consume the sequence, or observe different event instances from those the
/// Azure SDK eventually sends. This wrapper observes items only as the SDK enumerates them, preserving the source's
/// enumeration behavior. It is constructed dynamically because the tracer cannot reference the Azure SDK event types.
/// </remarks>
internal static class EventGridObservingEnumerable
{
    internal interface IObserver
    {
        void OnItem(object? item);

        void OnCompleted(int count, object? firstItem);
    }

    private interface IFactory
    {
        object Create(object events, IObserver observer);
    }

    internal static bool TryWrap<TEvents>(ref TEvents events, IObserver observer)
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
            var wrapperType = typeof(ObservingEnumerable<>).MakeGenericType(eventType);
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
        public object Create(object events, IObserver observer)
            => new ObservingEnumerable<TEvent>((IEnumerable<TEvent>)events, observer);
    }

    private sealed class ObservingEnumerable<TEvent> : IEnumerable<TEvent>
    {
        private readonly IEnumerable<TEvent> _source;
        private readonly IObserver _observer;

        public ObservingEnumerable(IEnumerable<TEvent> source, IObserver observer)
        {
            _source = source;
            _observer = observer;
        }

        public IEnumerator<TEvent> GetEnumerator() => Observe().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private IEnumerable<TEvent> Observe()
        {
            object? firstItem = null;
            var count = 0;

            foreach (var item in _source)
            {
                if (count == 0)
                {
                    firstItem = item;
                }

                count++;
                NotifyItem(item);
                yield return item;
            }

            NotifyCompleted(count, firstItem);
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

        private void NotifyCompleted(int count, object? firstItem)
        {
            try
            {
                _observer.OnCompleted(count, firstItem);
            }
            catch
            {
                // Instrumentation must not affect customer enumeration.
            }
        }
    }
}
