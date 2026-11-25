// <copyright file="EventGridObservingEnumerable.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

/// <summary>
/// Replaces a declared <see cref="IEnumerable{T}"/> argument with an observing wrapper without
/// taking a compile-time dependency on the enumerable's item type.
/// </summary>
/// <remarks>
/// Event Grid send APIs accept deferred or one-shot sequences. Enumerating them in the CallTarget begin callback
/// could execute customer code early, consume the sequence, or observe different event instances from those the
/// Azure SDK eventually sends. This wrapper observes items only as the SDK enumerates them, preserving the source's
/// enumeration timing and item flow. It is constructed dynamically because the tracer cannot reference the Azure SDK
/// event types.
/// </remarks>
internal static class EventGridObservingEnumerable
{
    internal interface IObserver
    {
        void OnItem(object? item);

        void OnEnumerationCompleted(int count, object? firstItem);
    }

    private interface IEnumerableWrapperFactory
    {
        object Create(object events, IObserver observer);
    }

    internal static TEvents Wrap<TEvents>(TEvents events, IObserver observer)
    {
        if ((object?)events is null || FactoryCache<TEvents>.Factory is not { } factory)
        {
            return events;
        }

        return (TEvents)factory.Create(events, observer);
    }

    private static IEnumerable<TEvent> Observe<TEvent>(IEnumerable<TEvent> events, IObserver observer)
    {
        object? firstItem = null;
        var count = 0;

        foreach (var item in events)
        {
            if (count == 0)
            {
                firstItem = item;
            }

            count++;
            try
            {
                observer.OnItem(item);
            }
            catch
            {
                // Instrumentation must not affect customer enumeration.
            }

            yield return item;
        }

        try
        {
            observer.OnEnumerationCompleted(count, firstItem);
        }
        catch
        {
            // Instrumentation must not affect customer enumeration.
        }
    }

    // CallTarget supplies TEvents as the method's declared parameter type, for example IEnumerable<CloudEvent>.
    // The tracer cannot reference CloudEvent directly, so reflection closes EnumerableWrapperFactory<TEvent> once.
    // The resulting factory is cached, and subsequent calls perform no reflection.
    private static class FactoryCache<TEvents>
    {
        public static readonly IEnumerableWrapperFactory? Factory = CreateFactory();

        private static IEnumerableWrapperFactory? CreateFactory()
        {
            var eventsType = typeof(TEvents);
            if (!eventsType.IsGenericType || eventsType.GetGenericTypeDefinition() != typeof(IEnumerable<>))
            {
                return null;
            }

            var eventType = eventsType.GetGenericArguments()[0];
            var factoryType = typeof(EnumerableWrapperFactory<>).MakeGenericType(eventType);
            return Activator.CreateInstance(factoryType, nonPublic: true) as IEnumerableWrapperFactory;
        }
    }

    private sealed class EnumerableWrapperFactory<TEvent> : IEnumerableWrapperFactory
    {
        public object Create(object events, IObserver observer)
            => Observe((IEnumerable<TEvent>)events, observer);
    }
}
