// <copyright file="ActivitySubclassDuckTypingTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Activity.DuckTypes;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Xunit;
using SD = System.Diagnostics;

namespace Datadog.Trace.Tests.Activity;

// Libraries such as Elasticsearch.Net subclass Activity, and the W3C fields IActivity binds to
// are private fields declared on the base Activity type
public class ActivitySubclassDuckTypingTests
{
    [Fact]
    public void Subclass_CanBeDuckTypedAsIActivity()
    {
        var activity = StartW3C(new DerivedActivity("derived"));
        try
        {
            var duck = activity.DuckCast<IActivity>();

            duck.TraceId.Should().Be(activity.TraceId.ToHexString());
            duck.SpanId.Should().Be(activity.SpanId.ToHexString());
        }
        finally
        {
            activity.Stop();
        }
    }

    [Fact]
    public void ChildOfSubclass_CanReadItsParent()
    {
        var parent = StartW3C(new DerivedActivity("parent"));
        var child = StartW3C(new SD.Activity("child"));
        try
        {
            child.DuckCast<IActivity>().Parent.Should().NotBeNull();
        }
        finally
        {
            child.Stop();
            parent.Stop();
        }
    }

    private static SD.Activity StartW3C(SD.Activity activity)
    {
        activity.SetIdFormat(SD.ActivityIdFormat.W3C);
        return activity.Start();
    }

    private class DerivedActivity : SD.Activity
    {
        public DerivedActivity(string operationName)
            : base(operationName)
        {
        }
    }
}
