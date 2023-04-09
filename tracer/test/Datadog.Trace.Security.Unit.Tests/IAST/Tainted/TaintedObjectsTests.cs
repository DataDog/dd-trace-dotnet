// <copyright file="TaintedObjectsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Iast;
using Xunit;
using Range = Datadog.Trace.Iast.Range;

namespace Datadog.Trace.Security.Unit.Tests.Iast.Tainted;

public class TaintedObjectsTests
{
    [Fact]
    public void GivenATaintedObject_WhenGet_ObjectIsRetrieved()
    {
        TaintedObjects taintedObjects = new();
        var ranges = new Range[] { new Range(1, 2, new Source(12, "name", "value")) };
        var taintedString = "tainted";
        taintedObjects.Taint(taintedString, ranges);
        var tainted2 = taintedObjects.Get(taintedString);
        Assert.Equal(taintedString, tainted2.Value);
    }

    [Fact]
    public void GivenATaintedString_WhenGet_StringAndSourceAreRetrieved()
    {
        TaintedObjects taintedObjects = new();
        var taintedString = "tainted";
        var source = new Source(12, "name", "value");
        taintedObjects.TaintInputString(taintedString, source);
        var tainted2 = taintedObjects.Get(taintedString);
        Assert.Equal(taintedString, tainted2.Value);
        Assert.Equal(source, tainted2.Ranges[0].Source);
    }

    [Fact]
    public void GivenATaintedObject_WhenPutEmptyString_ObjectIsNotRetrieved()
    {
        TaintedObjects taintedObjects = new();
        var ranges = new Range[] { new Range(1, 2, new Source(12, "name", "value")) };
        var taintedString = string.Empty;
        taintedObjects.Taint(taintedString, ranges);
        var tainted2 = taintedObjects.Get(taintedString);
        Assert.Null(tainted2);
    }

    [Fact]
    public void GivenATaintedString_WhenPutEmptyString_ObjectIsNotRetrieved()
    {
        TaintedObjects taintedObjects = new();
        var taintedString = string.Empty;
        taintedObjects.TaintInputString(taintedString, new Source(12, "name", "value"));
        var tainted2 = taintedObjects.Get(taintedString);
        Assert.Null(tainted2);
    }

    [Fact]
    public void GivenATaintedString_WhenPutNull_NoException()
    {
        TaintedObjects taintedObjects = new();
        taintedObjects.TaintInputString(null, new Source(12, "name", "value"));
        var tainted2 = taintedObjects.Get(null);
        Assert.Null(tainted2);
    }

    [Fact]
    public void GivenATaintedString_WhenGetSameValueDifferentReference_NullIsReturned()
    {
        TaintedObjects taintedObjects = new();
        var guid = new Guid();
        var tainted1 = guid.ToString();
        var tainted2 = guid.ToString();
        taintedObjects.TaintInputString(tainted1, new Source(12, "name", "value"));
        Assert.Equal(tainted1, taintedObjects.Get(tainted1).Value);
        Assert.Null(taintedObjects.Get(tainted2));
    }
}
