// <copyright file="DefaultTaintedMapTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.Iast;
using Xunit;
using Range = Datadog.Trace.Iast.Range;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class DefaultTaintedMapTests
{
    [Fact]
    public void GivenATaintedObject_WhenPutAndGet_ObjectIsRetrieved()
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
    public void GivenATaintedObject_WhenNotUsedAndPurge_ObjectIsNotRetrieved()
    {
        DefaultTaintedMap map = new();
        using (var testObject = new DisposableObject())
        {
            map.Put(new TaintedObject(testObject, null));
            System.Diagnostics.Debug.WriteLine("Memory used before collection:       {0:N0}", GC.GetTotalMemory(false));
            testObject.Dispose();
        }

        GC.Collect();
        GC.Collect();
        GC.Collect();
        System.Diagnostics.Debug.WriteLine("Memory used after full collection:   {0:N0}", GC.GetTotalMemory(true));
        GC.WaitForPendingFinalizers();
        map.Purge();
        Assert.Empty(map.GetReferenceQueue());
    }

    [Fact]
    public void GivenATaintedObject_WhenGetAfterPurge_ObjectIsRetrievedAndUnchanged()
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
    public void GivenATaintedObject_WhenClear_ObjectIsNotRetrieved()
    {
        DefaultTaintedMap map = new();
        var testObject = new object();
        var source = new Source(12, "name", "value");
        var tainted = new TaintedObject(testObject, new Range[] { new Range(1, 2, source) });
        map.Put(tainted);
        Assert.NotNull(map.Get(testObject));
        map.Clear();
        Assert.Null(map.Get(testObject));
        Assert.Empty(map.GetReferenceQueue());
    }

    [Fact]
    public void GivenATaintedObject_WhenPutManyObjects_LastObjectIsAlwaysRetrieved()
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
    public void GivenATaintedObject_WhenPutDefaultFlatModeThresoldElements_AllObjectsAreAlwaysRetrieved()
    {
        DefaultTaintedMap map = new();
        List<string> objects = new();

        for (int i = 0; i < DefaultTaintedMap.DefaultFlatModeThresold / 2; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(12, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            Assert.NotNull(map.Get(testString));
            objects.Add(testString);

            if (map.TableToList().Count != map.GetReferenceQueue().Count)
            {
                Assert.Equal(map.TableToList().Count, map.GetReferenceQueue().Count);
            }
        }

        map.Purge();
        Assert.Equal(DefaultTaintedMap.DefaultFlatModeThresold, map.GetReferenceQueue().Count);
        Assert.Equal(DefaultTaintedMap.DefaultFlatModeThresold, map.TableToList().Count);
        Assert.False(map.IsFlat);

        foreach (var item in objects)
        {
            Assert.NotNull(map.Get(item));
        }
    }

    [Fact]
    public void GivenATaintedObject_WhenPutMoreThanDefaultFlatModeThresoldElements_GetsFlatAndObjectsAreTheSame()
    {
        DefaultTaintedMap map = new();
        List<string> objects = new();

        for (int i = 0; i < DefaultTaintedMap.DefaultFlatModeThresold * 2; i++)
        {
            string testString = Guid.NewGuid().ToString();
            var source = new Source(12, "name", "value");
            var tainted = new TaintedObject(testString, new Range[] { new Range(1, 2, source) });
            map.Put(tainted);
            objects.Add(testString);
        }

        Assert.True(map.IsFlat);

        foreach (var itemInMap in map.TableToList())
        {
            Assert.Contains((itemInMap as TaintedObject).Value, objects);
        }
    }
}
