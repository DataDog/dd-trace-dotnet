// <copyright file="AllocationFreeEnumeratorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Activity.Helpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Activity.Helpers;

public class AllocationFreeEnumeratorTests
{
    [Fact]
    public void CanEnumerateActivityTags()
    {
        var activity = new System.Diagnostics.Activity("operation");
        activity.AddTag("key", "value");
        activity.AddTag("key2", "value2");

        var forEach = AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, string>>, KeyValuePair<string, string>, int>
           .BuildAllocationFreeForEachDelegate(activity.Tags.GetType());

        int invocations = 0;
        forEach(
            activity.Tags,
            ref invocations,
            (ref state, item) =>
            {
                state++;
                return true;
            });

        invocations.Should().Be(2);
    }

#if NET5_0_OR_GREATER
    [Fact]
    public void CanEnumerateActivityTagObjects()
    {
        var activity = new System.Diagnostics.Activity("operation");
        activity.AddTag("key", 23);
        activity.AddTag("key2", "value2");

        var forEach = AllocationFreeEnumerator<IEnumerable<KeyValuePair<string, object>>, KeyValuePair<string, object>, int>
           .BuildAllocationFreeForEachDelegate(activity.TagObjects.GetType());

        int invocations = 0;
        forEach(
            activity.TagObjects,
            ref invocations,
            (ref state, item) =>
            {
                state++;
                return true;
            });

        invocations.Should().Be(2);
    }
#endif

    [Fact]
    public void CanEnumerateList()
    {
        var list = Enumerable.Range(0, 100).ToList();

        var forEach = AllocationFreeEnumerator<IEnumerable<int>, int, long>
           .BuildAllocationFreeForEachDelegate(list.GetType());

        long sum = 0;
        forEach(
            list,
            ref sum,
            (ref state, item) =>
            {
                state += item;
                return true;
            });
        sum.Should().Be(list.Sum(x => x));
    }

    [Fact]
    public void CanEnumerateWhenNoNonInterfaceMethod()
    {
        var values = Enumerable.Range(0, 100).ToList();
        var list = new CustomList(values);

        var forEach = AllocationFreeEnumerator<IEnumerable<int>, int, long>
           .BuildAllocationFreeForEachDelegate(list.GetType());

        long sum = 0;
        forEach(
            list,
            ref sum,
            (ref state, item) =>
            {
                state += item;
                return true;
            });

        sum.Should().Be(list.Sum(x => x));
    }

    public class CustomList(List<int> list) : IEnumerable<int>
    {
        private readonly List<int> _list = list;

        public IEnumerator<int> GetEnumerator() => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        internal struct Enumerator(CustomList list) : IEnumerator<int>
        {
            private readonly CustomList _list = list;
            private int _index = -1;

            public int Current => _list._list[_index];

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _index++;
                return _list._list.Count > _index;
            }

            public void Reset() => throw new System.NotImplementedException();

            public void Dispose()
            {
            }
        }
    }
}
