// <copyright file="EventGridObservingEnumerableTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Azure.EventGrid;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.Azure.EventGrid;

public class EventGridObservingEnumerableTests
{
    [Fact]
    public void WrapsLazilyAndPreservesRepeatedEnumeration()
    {
        var enumerationCount = 0;
        var observer = new RecordingObserver();
        IEnumerable<TestEvent> events = CreateEvents();
        var originalEvents = events;

        events = EventGridObservingEnumerable.Wrap(events, observer);

        events.Should().NotBeSameAs(originalEvents);
        enumerationCount.Should().Be(0);

        var firstPass = events.ToArray();
        var secondPass = events.ToArray();

        enumerationCount.Should().Be(2);
        firstPass.Should().Equal(secondPass);
        firstPass[0].Should().NotBeSameAs(secondPass[0]);
        firstPass[1].Should().NotBeSameAs(secondPass[1]);
        observer.Items.Should().Equal(firstPass.Concat(secondPass));
        observer.CompletedCounts.Should().Equal(2, 2);
        observer.FirstItems.Should().Equal(firstPass[0], secondPass[0]);

        IEnumerable<TestEvent> CreateEvents()
        {
            enumerationCount++;
            yield return new TestEvent(1);
            yield return new TestEvent(2);
        }
    }

    [Fact]
    public void PreservesOneShotEnumerableBehavior()
    {
        var observer = new RecordingObserver();
        IEnumerable<int> events = new OneShotEnumerable<int>([1, 2, 3]);
        events = EventGridObservingEnumerable.Wrap(events, observer);

        events.Should().Equal(1, 2, 3);
        Action secondEnumeration = () => events.ToArray();

        secondEnumeration.Should().Throw<InvalidOperationException>();
        observer.Items.Should().Equal(1, 2, 3);
        observer.CompletedCounts.Should().Equal(3);
    }

    [Fact]
    public void DisposesTheSourceAfterPartialEnumeration()
    {
        var disposeCount = 0;
        var observer = new RecordingObserver();
        IEnumerable<int> events = CreateEvents();
        events = EventGridObservingEnumerable.Wrap(events, observer);

        using (var enumerator = events.GetEnumerator())
        {
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be(1);
        }

        disposeCount.Should().Be(1);
        observer.CompletedCounts.Should().BeEmpty();

        events.Should().Equal(1, 2, 3);
        disposeCount.Should().Be(2);
        observer.Items.Should().Equal(1, 1, 2, 3);
        observer.CompletedCounts.Should().Equal(3);

        IEnumerable<int> CreateEvents()
        {
            try
            {
                yield return 1;
                yield return 2;
                yield return 3;
            }
            finally
            {
                disposeCount++;
            }
        }
    }

    [Fact]
    public void PreservesSourceExceptionsAcrossEnumerations()
    {
        var enumerationCount = 0;
        var expectedException = new InvalidOperationException("Expected test exception");
        var observer = new RecordingObserver();
        IEnumerable<int> events = CreateEvents();
        events = EventGridObservingEnumerable.Wrap(events, observer);

        Action firstPass = () => events.ToArray();
        Action secondPass = () => events.ToArray();

        firstPass.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expectedException);
        secondPass.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expectedException);
        enumerationCount.Should().Be(2);
        observer.Items.Should().Equal(1, 1);
        observer.CompletedCounts.Should().BeEmpty();

        IEnumerable<int> CreateEvents()
        {
            enumerationCount++;
            yield return 1;
            throw expectedException;
        }
    }

    [Fact]
    public void ObserverFailuresDoNotAffectEnumeration()
    {
        IEnumerable<int> events = new[] { 1, 2, 3 };

        events = EventGridObservingEnumerable.Wrap(events, new ThrowingObserver());

        events.Should().Equal(1, 2, 3);
        events.Should().Equal(1, 2, 3);
    }

    private sealed record TestEvent(int Id);

    private sealed class RecordingObserver : EventGridObservingEnumerable.IObserver
    {
        public List<object?> Items { get; } = [];

        public List<int> CompletedCounts { get; } = [];

        public List<object?> FirstItems { get; } = [];

        public void OnItem(object? item) => Items.Add(item);

        public void OnCompleted(int count, object? firstItem)
        {
            CompletedCounts.Add(count);
            FirstItems.Add(firstItem);
        }
    }

    private sealed class ThrowingObserver : EventGridObservingEnumerable.IObserver
    {
        public void OnItem(object? item) => throw new InvalidOperationException("Expected observer exception");

        public void OnCompleted(int count, object? firstItem) => throw new InvalidOperationException("Expected observer exception");
    }

    private sealed class OneShotEnumerable<T> : IEnumerable<T>
    {
        private readonly IEnumerable<T> _items;
        private bool _enumerated;

        public OneShotEnumerable(IEnumerable<T> items)
        {
            _items = items;
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (_enumerated)
            {
                throw new InvalidOperationException("The enumerable can only be enumerated once.");
            }

            _enumerated = true;
            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
