// <copyright file="DefaultTaintedMapTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Text;
using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;
using Range = Datadog.Trace.Iast.Range;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class DefaultTaintedMapTests
{
    [Fact]
    public void GivenATaintedObjectMap_WhenPutAndGetString_ObjectIsRetrieved()
    {
        DefaultTaintedMap map = new();
        string testString = "test";
        var source = new Source(SourceType.RequestBody, "name", "value");
        var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
        map.Put(tainted);
        var tainted2 = map.Get(testString);
        tainted2.Should().NotBeNull();
        tainted2.Value.Should().Be(testString);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutAndGetObject_ObjectIsRetrieved()
    {
        DefaultTaintedMap map = new();
        var stringObject = new StringForTest("StringForTest");
        var tainted = new TaintedObject(stringObject, null);
        map.Put(tainted);
        var tainted2 = map.Get(stringObject);
        tainted2.Should().NotBeNull();
        stringObject.Should().Be(tainted2.Value);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutAndGetStringBuilder_ObjectIsRetrieved()
    {
        DefaultTaintedMap map = new();
        var text = new StringBuilder("test");
        var tainted = new TaintedObject(text, null);
        map.Put(tainted);
        var tainted2 = map.Get(text);
        tainted2.Should().NotBeNull();
        text.Should().Be(tainted2.Value);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutAndGetDifferentObjectSameValue_ObjectIsNotRetrieved()
    {
        DefaultTaintedMap map = new();
        var testGuid = Guid.NewGuid();
        var testString = testGuid.ToString();
        var testString2 = testGuid.ToString();
        map.Put(new TaintedObject(testString, null));
        var tainted2 = map.Get(testString2);
        tainted2.Should().BeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutEmptyString_ObjectIsNotInserted()
    {
        DefaultTaintedMap map = new();
        var tainted = new TaintedObject(string.Empty, null);
        map.Put(tainted);
        map.GetListValues().Should().BeEmpty();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutNullTaintedValue_ObjectIsNotInserted()
    {
        DefaultTaintedMap map = new();
        var tainted = new TaintedObject(null, null);
        map.Put(tainted);
        map.GetListValues().Should().BeEmpty();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutNull_ObjectIsNotInserted()
    {
        DefaultTaintedMap map = new();
        map.Put(null);
        map.GetListValues().Should().BeEmpty();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenGetNull_NoExceptionIsThrown()
    {
        DefaultTaintedMap map = new();
        map.Get(null).Should().BeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenGetAfterPurge_ObjectIsRetrievedAndUnchanged()
    {
        DefaultTaintedMap map = new();
        var testObject = new object();
        var tainted = new TaintedObject(testObject, new Range[] { new Range(1, 2, new Source(SourceType.RequestBody, "name", "value")) });
        map.Put(tainted);
        map.Purge();
        var tainted2 = map.Get(testObject);
        tainted2.Should().NotBeNull();
        tainted.Value.Should().Be(tainted2.Value);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenClear_ObjectIsNotRetrieved()
    {
        DefaultTaintedMap map = new();
        var testObject = new object();
        var source = new Source(SourceType.RequestBody, "name", "value");
        var tainted = new TaintedObject(testObject, new Range[] { new Range(1, 2, source) });
        map.Put(tainted);
        map.Get(testObject).Should().NotBeNull();
        map.Clear();
        map.Get(testObject).Should().BeNull();
        map.GetListValues().Should().BeEmpty();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutManyObjects_LastObjectIsAlwaysRetrieved()
    {
        DefaultTaintedMap map = new();

        for (int i = 0; i < DefaultTaintedMap.DefaultCapacity; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(SourceType.RequestBody, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            var tainted2 = map.Get(testString);
            tainted2.Should().NotBeNull();
            tainted.Value.Should().Be(tainted2.Value);
        }
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutDefaultFlatModeThresoldElements_AllObjectsAreAlwaysRetrieved()
    {
        DefaultTaintedMap map = new();
        List<string> objects = new();
        var iterations = DefaultTaintedMap.DefaultFlatModeThresold / 2;

        for (int i = 0; i < iterations; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(SourceType.RequestBody, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            map.Get(testString).Should().NotBeNull();
            objects.Add(testString);
        }

        map.Purge();
        iterations.Should().Be(map.GetListValues().Count);
        map.IsFlat.Should().BeFalse();
        AssertContained(map, objects);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutMoreThanDefaultFlatModeThresoldElements_GetsFlatAndObjectsAreTheSame()
    {
        List<string> objects = new();
        DefaultTaintedMap map = new();
        AssertFlatMode(map, objects);

        foreach (var itemInMap in map.GetListValues())
        {
            objects.Should().Contain((itemInMap as TaintedObject).Value as string);
        }
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutMoreThanDefaultFlatModeThresoldElements_GetsFlatAndOnlyOneObjectWithSameHashIsStored()
    {
        List<string> objects = new();
        DefaultTaintedMap map = new();
        AssertFlatMode(map, objects);

        var testString = new StringForTest(Guid.NewGuid().ToString());
        testString.Hash = 10;
        map.Put(new TaintedForTest(testString, null));

        map.Get(testString).Should().NotBeNull();

        var testString2 = new StringForTest(Guid.NewGuid().ToString());
        testString2.Hash = 10;
        map.Put(new TaintedForTest(testString2, null));

        map.Get(testString2).Should().NotBeNull();
        map.Get(testString).Should().BeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenFlatMode_LastElementIsStored()
    {
        List<string> objects = new();
        DefaultTaintedMap map = new();
        AssertFlatMode(map, objects);

        for (int i = 0; i < 100; i++)
        {
            var testString = new StringForTest(Guid.NewGuid().ToString());
            map.Put(new TaintedForTest(testString, null));
            map.Get(testString).Should().NotBeNull();
        }
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutObjectsThatGetDisposed_ObjectsArePurged()
    {
        DefaultTaintedMap map = new();
        List<string> disposedObjects = new();
        List<string> aliveObjects = new();
        bool alive = true;

        for (int i = 0; i < DefaultTaintedMap.DefaultFlatModeThresold / 2; i++)
        {
            var testString = Guid.NewGuid().ToString();
            var tainted = new TaintedForTest(testString, null);
            map.Put(tainted);
            (alive ? aliveObjects : disposedObjects).Add(testString);
            alive = !alive;
        }

        AssertContained(map, aliveObjects);
        AssertContained(map, disposedObjects);

        foreach (var item in disposedObjects)
        {
            (map.Get(item) as TaintedForTest).SetAlive(false);
        }

        map.Purge();
        AssertContained(map, aliveObjects);
        AssertNotContained(map, disposedObjects);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenASingleObjectNotDisposed_IsNotPurged()
    {
        DefaultTaintedMap map = new();
        var testString = Guid.NewGuid().ToString();
        var tainted = new TaintedForTest(testString, null);
        map.Put(tainted);
        map.Get(testString).Should().NotBeNull();
        map.Purge();
        map.Get(testString).Should().NotBeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenASingleObjectDisposed_IsPurged()
    {
        DefaultTaintedMap map = new();
        var testString = Guid.NewGuid().ToString();
        var tainted = new TaintedForTest(testString, null);
        map.Put(tainted);
        map.Get(testString).Should().NotBeNull();
        (map.Get(testString) as TaintedForTest).SetAlive(false);
        map.Purge();
        map.Get(testString).Should().BeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenHasCausesPurge_IsPurged()
    {
        DefaultTaintedMap map = new();

        for (int i = 0; i < 10; i++)
        {
            var testString = new StringForTest(Guid.NewGuid().ToString());
            testString.Hash = 1;
            var tainted = new TaintedForTest(testString, null);
            map.Put(tainted);
            tainted.SetAlive(false);
            map.Get(testString).Should().NotBeNull();
        }

        map.GetListValues().Should().HaveCount(10);
        var testStringNew = new StringForTest(Guid.NewGuid().ToString());
        testStringNew.Hash = 0;
        var taintedObject = new TaintedForTest(testStringNew, null);
        map.Put(taintedObject);
        map.GetListValues().Should().HaveCount(1);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 3)]
    [InlineData(50, 0)]
    [InlineData(50, 49)]
    [InlineData(50, 25)]
    public void GivenATaintedObjectMap_WhenDisposedInSameHashPosition_IsPurged(int totalObjects, int disposedIndex)
    {
        DefaultTaintedMap map = new();
        List<StringForTest> addedObjects = new();

        for (int i = 0; i < totalObjects; i++)
        {
            var testString = new StringForTest(i.ToString());
            testString.Hash = 10;
            var tainted = new TaintedForTest(testString, null);
            map.Put(tainted);
            addedObjects.Add(testString);
        }

        (map.Get(addedObjects[disposedIndex]) as TaintedForTest).SetAlive(false);
        map.Purge();

        for (int i = 0; i < totalObjects; i++)
        {
            if (i == disposedIndex)
            {
                map.Get(addedObjects[i]).Should().BeNull();
            }
            else
            {
                map.Get(addedObjects[i]).Should().NotBeNull();
            }
        }
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopExistingObject_ObjectIsRemovedAndReturned()
    {
        var map = new DefaultTaintedMap();
        var testString = "test";
        var source = new Source(SourceType.RequestBody, "name", "value");
        var tainted = new TaintedObject(testString, [new Range(0, 4, source)]);
        map.Put(tainted);

        var popped = map.Pop(testString);

        popped.Should().NotBeNull();
        popped!.Value.Should().Be(testString);
        map.Get(testString).Should().BeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopNonExistingObject_NullIsReturned()
    {
        var map = new DefaultTaintedMap();
        var testString = "test";
        var nonExistingString = "nonexistent";
        var source = new Source(SourceType.RequestBody, "name", "value");
        var tainted = new TaintedObject(testString, [new Range(0, 4, source)]);
        map.Put(tainted);

        var popped = map.Pop(nonExistingString);

        popped.Should().BeNull();
        map.Get(testString).Should().NotBeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopMultipleTimes_ObjectIsRemovedOnlyOnce()
    {
        var map = new DefaultTaintedMap();
        var testString = "test";
        var source = new Source(SourceType.RequestBody, "name", "value");
        var tainted = new TaintedObject(testString, [new Range(0, 4, source)]);
        map.Put(tainted);

        var firstPop = map.Pop(testString);
        var secondPop = map.Pop(testString);

        firstPop.Should().NotBeNull();
        secondPop.Should().BeNull();
        map.Get(testString).Should().BeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopWithMultipleEntriesSameHash_ObjectIsRemoved()
    {
        var map = new DefaultTaintedMap();
        var testString1 = new StringForTest("test1") { Hash = 42 };
        var testString2 = new StringForTest("test2") { Hash = 42 };
        var source = new Source(SourceType.RequestBody, "name", "value");

        var tainted1 = new TaintedForTest(testString1, [new Range(0, 5, source)]);
        var tainted2 = new TaintedForTest(testString2, [new Range(0, 5, source)]);

        map.Put(tainted1);
        map.Put(tainted2);

        var popped = map.Pop(testString1);

        popped.Should().NotBeNull();
        popped!.Value.Should().Be(testString1);
        map.Get(testString1).Should().BeNull();
        map.Get(testString2).Should().NotBeNull();
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopMiddleEntryInChain_OtherEntriesRemainAccessible()
    {
        var map = new DefaultTaintedMap();
        var source = new Source(SourceType.RequestBody, "name", "value");

        var testString1 = new StringForTest("test1") { Hash = 42 }; // Tail of the chain
        var testString2 = new StringForTest("test2") { Hash = 42 }; // Middle of the chain
        var testString3 = new StringForTest("test3") { Hash = 42 }; // Head of the chain

        var tainted1 = new TaintedForTest(testString1, [new Range(0, 5, source)]);
        var tainted2 = new TaintedForTest(testString2, [new Range(0, 5, source)]);
        var tainted3 = new TaintedForTest(testString3, [new Range(0, 5, source)]);

        map.Put(tainted1); // This will be at the tail
        map.Put(tainted2); // Middle
        map.Put(tainted3); // Head

        var popped = map.Pop(testString2); // Remove the middle entry

        popped.Should().NotBeNull();
        popped!.Value.Should().Be(testString2);

        map.Get(testString1).Should().NotBeNull();
        map.Get(testString3).Should().NotBeNull();
        map.Get(testString2).Should().BeNull();

        map.Get(testString1)!.Value.Should().Be(testString1);
        map.Get(testString3)!.Value.Should().Be(testString3);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopTailEntryInChain_OtherEntriesRemainAccessible()
    {
        var map = new DefaultTaintedMap();
        var source = new Source(SourceType.RequestBody, "name", "value");

        var testString1 = new StringForTest("test1") { Hash = 42 }; // Tail of the chain
        var testString2 = new StringForTest("test2") { Hash = 42 }; // Middle
        var testString3 = new StringForTest("test3") { Hash = 42 }; // Head

        var tainted1 = new TaintedForTest(testString1, [new Range(0, 5, source)]);
        var tainted2 = new TaintedForTest(testString2, [new Range(0, 5, source)]);
        var tainted3 = new TaintedForTest(testString3, [new Range(0, 5, source)]);

        map.Put(tainted1); // Tail
        map.Put(tainted2); // Middle
        map.Put(tainted3); // Head

        var popped = map.Pop(testString1); // Remove the tail entry

        popped.Should().NotBeNull();
        popped!.Value.Should().Be(testString1);

        map.Get(testString2).Should().NotBeNull();
        map.Get(testString3).Should().NotBeNull();
        map.Get(testString1).Should().BeNull();

        map.Get(testString2)!.Value.Should().Be(testString2);
        map.Get(testString3)!.Value.Should().Be(testString3);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopHeadEntryInChain_ChainHeadIsUpdatedAndOtherEntriesRemainAccessible()
    {
        var map = new DefaultTaintedMap();
        var source = new Source(SourceType.RequestBody, "name", "value");

        var testString1 = new StringForTest("test1") { Hash = 42 }; // Tail
        var testString2 = new StringForTest("test2") { Hash = 42 }; // Middle
        var testString3 = new StringForTest("test3") { Hash = 42 }; // Head

        var tainted1 = new TaintedForTest(testString1, [new Range(0, 5, source)]);
        var tainted2 = new TaintedForTest(testString2, [new Range(0, 5, source)]);
        var tainted3 = new TaintedForTest(testString3, [new Range(0, 5, source)]);

        map.Put(tainted1); // Tail
        map.Put(tainted2); // Middle
        map.Put(tainted3); // Head

        var popped = map.Pop(testString3); // Remove the head entry

        popped.Should().NotBeNull();
        popped!.Value.Should().Be(testString3);

        map.Get(testString1).Should().NotBeNull();
        map.Get(testString2).Should().NotBeNull();
        map.Get(testString3).Should().BeNull();

        map.Get(testString1)!.Value.Should().Be(testString1);
        map.Get(testString2)!.Value.Should().Be(testString2);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPopLastEntryInChain_MapDoesNotContainTheObjectAnymore()
    {
        var map = new DefaultTaintedMap();
        var testString = "test";
        var source = new Source(SourceType.RequestBody, "name", "value");
        var tainted = new TaintedObject(testString, [new Range(0, 4, source)]);

        map.Put(tainted);

        var popped = map.Pop(testString);

        popped.Should().NotBeNull();
        popped!.Value.Should().Be(testString);

        map.Get(testString).Should().BeNull();

        map.GetEstimatedSize().Should().Be(0);
    }

    private static void AssertNotContained(DefaultTaintedMap map, List<string> objects)
    {
        foreach (var item in objects)
        {
            map.Get(item).Should().BeNull();
        }
    }

    private static void AssertContained(DefaultTaintedMap map, List<string> objects)
    {
        foreach (var item in objects)
        {
            map.Get(item).Should().NotBeNull();
        }
    }

    private static void AssertFlatMode(DefaultTaintedMap map, List<string> objects)
    {
        for (int i = 0; i < DefaultTaintedMap.DefaultFlatModeThresold * 2; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(SourceType.RequestBody, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            objects.Add(testString);
        }

        map.IsFlat.Should().BeTrue();
    }
}
