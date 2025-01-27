// <copyright file="SecurityCoordinatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using FluentAssertions;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
#endif
using Moq;
using Xunit;
using static Datadog.Trace.AppSec.Coordinator.SecurityCoordinator;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SecurityCoordinatorTests
    {
        [Fact]
        public void DefaultBehavior()
        {
            var target = new AppSec.Security();
            var span = new Span(new SpanContext(1, 1), new System.DateTimeOffset());
            var secCoord = SecurityCoordinator.TryGet(target, span);
            secCoord.Should().BeNull();
        }

#if NETCOREAPP
        [Fact]
        public void GivenHttpTransportInstanceWithDisposedContext_WhenGetContextUninitialized_ThenResultIsTrue()
        {
            var contextMoq = new Mock<HttpContext>();
            contextMoq.Setup(x => x.Features).Throws(new ObjectDisposedException("Test exception"));
            var context = contextMoq.Object;
            HttpTransport transport = new(context);
            transport.GetAdditiveContext().Should().BeNull();
            transport.IsAdditiveContextDisposed().Should().BeTrue();
        }

        [Fact]
        public void GivenSecurityCoordinatorInstanceWithDisposedContext_WheRunWaf_ThenResultIsNull()
        {
            var contextMoq = new Mock<HttpContext>();
            contextMoq.Setup(x => x.Features).Throws(new ObjectDisposedException("Test exception"));
            var context = contextMoq.Object;
            CoreHttpContextStore.Instance.Set(context);
            var span = new Span(new SpanContext(1, 1), new DateTimeOffset());
            var securityCoordinator = SecurityCoordinator.TryGet(AppSec.Security.Instance, span);
            var result = securityCoordinator.Value.RunWaf(new(), runWithEphemeral: true, isRasp: true);
            result.Should().BeNull();
        }
#endif

#if NETFRAMEWORK
        [Fact]
        public void GivenSecurityCoordinatorInstanceWithResponseHeadersWritten_WheBlock_ThenBlockExceptionAndNoError()
        {
            var span = new Span(new SpanContext(1, 1), new DateTimeOffset());
            TextWriter textWriter = new StringWriter();
            var response = new HttpResponse(textWriter);
            response.StatusCode = 200;

            // set response.HeadersWritten = true by reflection. The setter is internal.
            var headersWrittenProperty = response.GetType().GetProperty("HeadersWritten", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            headersWrittenProperty.SetValue(response, true);

            HttpContext.Current = new HttpContext(new HttpRequest("file", "http://localhost/benchmarks", "data=param"), response);
            var securityCoordinator = SecurityCoordinator.TryGet(AppSec.Security.Instance, span);
            securityCoordinator.Should().NotBeNull();
            var resultMock = new Mock<IResult>();
            resultMock.SetupGet(x => x.ShouldBlock).Returns(true);
            resultMock.SetupGet(x => x.BlockInfo).Returns(new Dictionary<string, object>());

            try
            {
                securityCoordinator?.ReportAndBlock(resultMock.Object);
                Assert.Fail("Expected BlockException");
            }
            catch (BlockException)
            {
                // We cannot change the status code after the response has been written
                response.StatusCode.Should().Be(200);
            }
        }
#endif
    }
}
