// <copyright file="ActivityLinkConverterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Activity.DuckTypes;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests;

[Collection(nameof(ActivityTestsCollection))]
public class ActivityLinkConverterTests
{
    [Fact]
    public void LinkWithAllData_Should_Serialize()
    {
        var linkMock = new Mock<IActivityLink>();
        var contextMock = new Mock<IActivityContext>();
        var traceIdMock = new Mock<IActivityTraceId>();
        var spanIdMock = new Mock<IActivitySpanId>();

        contextMock.Setup(x => x.TraceId).Returns(traceIdMock.Object);
        contextMock.Setup(x => x.SpanId).Returns(spanIdMock.Object);
        linkMock.Setup(x => x.Context).Returns(contextMock.Object);
    }
}
