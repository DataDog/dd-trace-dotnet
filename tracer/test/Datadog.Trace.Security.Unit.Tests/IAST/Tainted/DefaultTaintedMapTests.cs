// <copyright file="DefaultTaintedMapTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.Iast;
using Xunit;
using Range = Datadog.Trace.Iast.Range;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class DefaultTaintedMapTests
{
    [Fact]
    public void GivenATaintedObjectMap_WhenPutAndGet_ObjectIsRetrieved()
    {
        DefaultTaintedMap map = new();
        string testString = "test";
        var source = new Source(12, "name", "value");
        var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
        map.Put(tainted);
        var tainted2 = map.Get(testString);
        Assert.NotNull(tainted2);
        Assert.Equal(tainted.GetHashCode(), tainted2.GetHashCode());
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutEmptyString_ObjectIsNotInserted()
    {
        DefaultTaintedMap map = new();
        var tainted = new TaintedObject(string.Empty, null);
        map.Put(tainted);
        Assert.Empty(map.ToList());
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutNull_ObjectIsNotInserted()
    {
        DefaultTaintedMap map = new();
        map.Put(null);
        Assert.Empty(map.ToList());
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenGetNull_NoExceptionIsThrown()
    {
        DefaultTaintedMap map = new();
        Assert.Null(map.Get(null));
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenGetAfterPurge_ObjectIsRetrievedAndUnchanged()
    {
        DefaultTaintedMap map = new();
        var testObject = new object();
        var tainted = new TaintedObject(testObject, new Range[] { new Range(1, 2, new Source(12, "name", "value")) });
        var hash1 = tainted.GetHashCode();
        map.Put(tainted);
        map.Purge();
        var tainted2 = map.Get(testObject);
        Assert.NotNull(tainted2);
        Assert.Equal(hash1, tainted2.GetHashCode());
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenClear_ObjectIsNotRetrieved()
    {
        DefaultTaintedMap map = new();
        var testObject = new object();
        var source = new Source(12, "name", "value");
        var tainted = new TaintedObject(testObject, new Range[] { new Range(1, 2, source) });
        map.Put(tainted);
        Assert.NotNull(map.Get(testObject));
        map.Clear();
        Assert.Null(map.Get(testObject));
        Assert.Empty(map.ToList());
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutManyObjects_LastObjectIsAlwaysRetrieved()
    {
        DefaultTaintedMap map = new();

        for (int i = 0; i < DefaultTaintedMap.DefaultCapacity; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(12, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            var tainted2 = map.Get(testString);
            Assert.NotNull(tainted2);
            Assert.Equal(tainted.GetHashCode(), tainted2.GetHashCode());
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
            var source = new Source(12, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            Assert.NotNull(map.Get(testString));
            objects.Add(testString);
        }

        map.Purge();
        Assert.Equal(iterations, map.ToList().Count);
        Assert.False(map.IsFlat);
        AssertContained(map, objects);
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenPutMoreThanDefaultFlatModeThresoldElements_GetsFlatAndObjectsAreTheSame()
    {
        List<string> objects = new();
        DefaultTaintedMap map = new();
        AssertFlatMode(map, objects);

        foreach (var itemInMap in map.ToList())
        {
            Assert.Contains((itemInMap as TaintedObject).Value, objects);
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

        Assert.NotNull(map.Get(testString));

        var testString2 = new StringForTest(Guid.NewGuid().ToString());
        testString2.Hash = 10;
        map.Put(new TaintedForTest(testString2, null));

        Assert.NotNull(map.Get(testString2));
        Assert.Null(map.Get(testString));
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
        Assert.NotNull(map.Get(testString));
        map.Purge();
        Assert.NotNull(map.Get(testString));
    }

    [Fact]
    public void GivenATaintedObjectMap_WhenASingleObjectDisposed_IsPurged()
    {
        DefaultTaintedMap map = new();
        var testString = Guid.NewGuid().ToString();
        var tainted = new TaintedForTest(testString, null);
        map.Put(tainted);
        Assert.NotNull(map.Get(testString));
        (map.Get(testString) as TaintedForTest).SetAlive(false);
        map.Purge();
        Assert.Null(map.Get(testString));
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(4, 1)]
    [InlineData(4, 2)]
    [InlineData(4, 3)]
    [InlineData(50, 0)]
    [InlineData(50, 49)]
    [InlineData(50, 25)]
    public void GivenATaintedObjectMap_WhenDisposedInSameHashPosition0_IsPurged(int totalObjects, int indexDisposed)
    {
        TestObjectPurgeSameHash(totalObjects, indexDisposed);
    }

    private static void TestObjectPurgeSameHash(int totalObjects, int disposedIndex)
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
                Assert.Null(map.Get(addedObjects[i]));
            }
            else
            {
                Assert.NotNull(map.Get(addedObjects[i]));
            }
        }
    }

    private static void AssertNotContained(DefaultTaintedMap map, List<string> objects)
    {
        foreach (var item in objects)
        {
            Assert.Null(map.Get(item));
        }
    }

    private static void AssertContained(DefaultTaintedMap map, List<string> objects)
    {
        foreach (var item in objects)
        {
            Assert.NotNull(map.Get(item));
        }
    }

    private static void AssertFlatMode(DefaultTaintedMap map, List<string> objects)
    {
        for (int i = 0; i < DefaultTaintedMap.DefaultFlatModeThresold * 2; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(12, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            objects.Add(testString);
        }

        Assert.True(map.IsFlat);
    }
}
