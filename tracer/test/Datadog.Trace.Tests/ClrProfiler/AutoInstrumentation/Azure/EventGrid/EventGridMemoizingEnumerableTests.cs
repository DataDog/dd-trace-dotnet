// <copyright file="EventGridMemoizingEnumerableTests.cs" company="Datadog">
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

public class EventGridMemoizingEnumerableTests
{
    [Fact]
    public void WrapsLazilyAndReplaysTheSameItems()
    {
        var enumerationCount = 0;
        var observer = new RecordingObserver();
        IEnumerable<TestEvent> events = CreateEvents();
        var originalEvents = events;

        EventGridMemoizingEnumerable.TryWrap(ref events, observer).Should().BeTrue();

        events.Should().NotBeSameAs(originalEvents);
        enumerationCount.Should().Be(0);

        var firstPass = events.ToArray();
        var secondPass = events.ToArray();

        enumerationCount.Should().Be(1);
        firstPass.Should().Equal(secondPass);
        firstPass[0].Should().BeSameAs(secondPass[0]);
        firstPass[1].Should().BeSameAs(secondPass[1]);
        observer.Items.Should().Equal(firstPass);
        observer.CompletedCounts.Should().Equal(2);

        IEnumerable<TestEvent> CreateEvents()
        {
            enumerationCount++;
            yield return new TestEvent(1);
            yield return new TestEvent(2);
        }
    }

    [Fact]
    public void ReplaysAOneShotEnumerable()
    {
        var observer = new RecordingObserver();
        IEnumerable<int> events = new OneShotEnumerable<int>([1, 2, 3]);

        EventGridMemoizingEnumerable.TryWrap(ref events, observer).Should().BeTrue();

        events.Should().Equal(1, 2, 3);
        events.Should().Equal(1, 2, 3);
        observer.Items.Should().Equal(1, 2, 3);
        observer.CompletedCounts.Should().Equal(3);
    }

    [Fact]
    public void ContinuesTheSourceAfterAPartialEnumeration()
    {
        var observer = new RecordingObserver();
        IEnumerable<int> events = new OneShotEnumerable<int>([1, 2, 3]);
        EventGridMemoizingEnumerable.TryWrap(ref events, observer).Should().BeTrue();

        using (var enumerator = events.GetEnumerator())
        {
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be(1);
        }

        events.Should().Equal(1, 2, 3);
        events.Should().Equal(1, 2, 3);
        observer.Items.Should().Equal(1, 2, 3);
        observer.CompletedCounts.Should().Equal(3);
    }

    [Fact]
    public void ReplaysTheSourceExceptionWithoutEnumeratingAgain()
    {
        var enumerationCount = 0;
        var expectedException = new InvalidOperationException("Expected test exception");
        var observer = new RecordingObserver();
        IEnumerable<int> events = CreateEvents();
        EventGridMemoizingEnumerable.TryWrap(ref events, observer).Should().BeTrue();

        Action firstPass = () => events.ToArray();
        Action secondPass = () => events.ToArray();

        firstPass.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expectedException);
        secondPass.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expectedException);
        enumerationCount.Should().Be(1);
        observer.Items.Should().Equal(1);
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
        IEnumerable<int> events = new OneShotEnumerable<int>([1, 2, 3]);

        EventGridMemoizingEnumerable.TryWrap(ref events, new ThrowingObserver()).Should().BeTrue();

        events.Should().Equal(1, 2, 3);
        events.Should().Equal(1, 2, 3);
    }

    private sealed record TestEvent(int Id);

    private sealed class RecordingObserver : IEventGridMemoizingEnumerableObserver
    {
        public List<object?> Items { get; } = [];

        public List<int> CompletedCounts { get; } = [];

        public void OnItem(object? item) => Items.Add(item);

        public void OnCompleted(int count) => CompletedCounts.Add(count);
    }

    private sealed class ThrowingObserver : IEventGridMemoizingEnumerableObserver
    {
        public void OnItem(object? item) => throw new InvalidOperationException("Expected observer exception");

        public void OnCompleted(int count) => throw new InvalidOperationException("Expected observer exception");
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
