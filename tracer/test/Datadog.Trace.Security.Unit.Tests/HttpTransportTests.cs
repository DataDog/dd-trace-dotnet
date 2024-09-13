// <copyright file="HttpTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using FluentAssertions;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
#endif
using Moq;
using Xunit;
using static Datadog.Trace.AppSec.Coordinator.SecurityCoordinator;

namespace Datadog.Trace.Security.Unit.Tests;

public class HttpTransportTests
{
    [Fact]
    public void Given_http_transport()
    {
        // Arrange
        var httpContext = CreateContext();
        var transport = new HttpTransport(httpContext);
        transport.IsBlocked.Should().BeFalse();
        transport.MarkBlocked();
        transport.IsBlocked.Should().BeTrue();
    }

    private HttpContext CreateContext()
    {
#if NETCOREAPP
        var contextMock = new Mock<HttpContext>();
        contextMock.SetupGet(c => c.Items).Returns(new Dictionary<object, object>());
        return contextMock.Object;
#else
        var request = new HttpRequest("file", "http://localhost/benchmarks", "data=param");
        var response = new HttpResponse(new StringWriter());
        var context = new HttpContext(request, response);
        return context;
#endif
    }
}
