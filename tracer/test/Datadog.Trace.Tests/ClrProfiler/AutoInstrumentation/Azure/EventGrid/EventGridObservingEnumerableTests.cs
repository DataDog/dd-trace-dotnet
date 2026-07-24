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
    public void WrapDoesNotEnumerateSource()
    {
        var enumerationCount = 0;
        var observer = new RecordingObserver();
        IEnumerable<int> source = CreateEvents();

        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        wrapped.Should().NotBeSameAs(source);
        enumerationCount.Should().Be(0);
        observer.Items.Should().BeEmpty();
        observer.Completions.Should().BeEmpty();

        IEnumerable<int> CreateEvents()
        {
            enumerationCount++;
            yield return 1;
        }
    }

    [Fact]
    public void FullEnumerationYieldsOriginalItemsAndReportsCompletion()
    {
        var firstEvent = new TestEvent(1);
        var secondEvent = new TestEvent(2);
        IEnumerable<TestEvent> source = new[] { firstEvent, secondEvent };
        var observer = new RecordingObserver();

        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        var result = wrapped.ToArray();

        result.Should().HaveCount(2);
        result[0].Should().BeSameAs(firstEvent);
        result[1].Should().BeSameAs(secondEvent);
        observer.Items.Should().HaveCount(2);
        observer.Items[0].Should().BeSameAs(firstEvent);
        observer.Items[1].Should().BeSameAs(secondEvent);
        observer.Completions.Should().ContainSingle();
        observer.Completions[0].Count.Should().Be(2);
        observer.Completions[0].FirstItem.Should().BeSameAs(firstEvent);
    }

    [Fact]
    public void EachEnumerationDelegatesToSource()
    {
        var enumerationCount = 0;
        var observer = new RecordingObserver();
        IEnumerable<TestEvent> source = CreateEvents();
        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        var firstPass = wrapped.ToArray();
        var secondPass = wrapped.ToArray();

        enumerationCount.Should().Be(2);
        firstPass.Select(item => item.Id).Should().Equal(1, 2);
        secondPass.Select(item => item.Id).Should().Equal(1, 2);
        firstPass[0].Should().NotBeSameAs(secondPass[0]);
        firstPass[1].Should().NotBeSameAs(secondPass[1]);
        observer.Completions.Select(completion => completion.Count).Should().Equal(2, 2);

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
        IEnumerable<int> source = new OneShotEnumerable<int>([1, 2, 3]);
        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        var firstResult = wrapped.ToArray();
        Action secondEnumeration = () => wrapped.ToArray();

        firstResult.Should().Equal(1, 2, 3);
        secondEnumeration.Should().Throw<InvalidOperationException>();
        observer.Items.Should().Equal(1, 2, 3);
        observer.Completions.Select(completion => completion.Count).Should().Equal(3);
    }

    [Fact]
    public void PartialEnumerationDisposesSourceWithoutReportingCompletion()
    {
        var disposeCount = 0;
        var observer = new RecordingObserver();
        IEnumerable<int> source = CreateEvents();
        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        using (var enumerator = wrapped.GetEnumerator())
        {
            enumerator.MoveNext().Should().BeTrue();
            enumerator.Current.Should().Be(1);
        }

        disposeCount.Should().Be(1);
        observer.Items.Should().Equal(1);
        observer.Completions.Should().BeEmpty();

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
    public void SourceExceptionPropagatesWithoutReportingCompletion()
    {
        var enumerationCount = 0;
        var expectedException = new InvalidOperationException("Expected test exception");
        var observer = new RecordingObserver();
        IEnumerable<int> source = CreateEvents();
        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        Action enumerate = () => wrapped.ToArray();

        enumerate.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(expectedException);
        enumerationCount.Should().Be(1);
        observer.Items.Should().Equal(1);
        observer.Completions.Should().BeEmpty();

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
        IEnumerable<int> source = new[] { 1, 2, 3 };
        var wrapped = EventGridObservingEnumerable.Wrap(source, new ThrowingObserver());

        var result = wrapped.ToArray();

        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void EmptyEnumerationReportsZeroItems()
    {
        IEnumerable<TestEvent> source = [];
        var observer = new RecordingObserver();
        var wrapped = EventGridObservingEnumerable.Wrap(source, observer);

        var result = wrapped.ToArray();

        result.Should().BeEmpty();
        observer.Items.Should().BeEmpty();
        observer.Completions.Should().ContainSingle();
        observer.Completions[0].Count.Should().Be(0);
        observer.Completions[0].FirstItem.Should().BeNull();
    }

    [Fact]
    public void ReturnsOriginalInputWhenWrappingIsUnsupported()
    {
        var observer = new RecordingObserver();
        IEnumerable<int>? nullSource = null;
        var concreteSource = new List<int> { 1, 2, 3 };

        var wrappedNull = EventGridObservingEnumerable.Wrap(nullSource, observer);
        var wrappedConcrete = EventGridObservingEnumerable.Wrap(concreteSource, observer);

        wrappedNull.Should().BeNull();
        wrappedConcrete.Should().BeSameAs(concreteSource);
        observer.Items.Should().BeEmpty();
        observer.Completions.Should().BeEmpty();
    }

    private sealed record TestEvent(int Id);

    private sealed class RecordingObserver : EventGridObservingEnumerable.IObserver
    {
        public List<object?> Items { get; } = [];

        public List<Completion> Completions { get; } = [];

        public void OnItem(object? item) => Items.Add(item);

        public void OnEnumerationCompleted(int count, object? firstItem) => Completions.Add(new Completion(count, firstItem));
    }

    private sealed record Completion(int Count, object? FirstItem);

    private sealed class ThrowingObserver : EventGridObservingEnumerable.IObserver
    {
        public void OnItem(object? item) => throw new InvalidOperationException("Expected observer exception");

        public void OnEnumerationCompleted(int count, object? firstItem) => throw new InvalidOperationException("Expected observer exception");
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
