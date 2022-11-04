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
        var source = new Source(12, "name", "value");
        var ranges = new Range[] { new Range(1, 2, source) };
        var taintedString = "tainted";
        taintedObjects.Taint(taintedString, ranges);
        var tainted2 = taintedObjects.Get(taintedString);
        Assert.Equal(taintedString, tainted2.Value);
    }
}
