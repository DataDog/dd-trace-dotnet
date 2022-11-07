// <copyright file="TaintedObjectsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using Xunit;

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
        Assert.Equal(source, tainted2.GetRanges()[0].Source);
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
        var source = new Source(12, "name", "value");
        taintedObjects.TaintInputString(taintedString, source);
        var tainted2 = taintedObjects.Get(taintedString);
        Assert.Null(tainted2);
    }
}
