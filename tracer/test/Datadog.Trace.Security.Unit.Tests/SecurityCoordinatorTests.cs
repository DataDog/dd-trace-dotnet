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
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Configuration;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
#if NETCOREAPP
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
#endif
using Moq;
using Xunit;
using static Datadog.Trace.AppSec.Coordinator.SecurityCoordinator;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class SecurityCoordinatorTests : WafLibraryRequiredTest
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
        public void GivenSecurityCoordinatorInstanceWithDisposedContext_WheRunWaf_ThenResultIsNull()
        {
            var contextMoq = new Mock<HttpContext>();
            contextMoq.Setup(x => x.Features).Throws(new ObjectDisposedException("Test exception"));
            var context = contextMoq.Object;
            CoreHttpContextStore.Instance.Set(context);
            var traceContext = new TraceContext(new EmptyDatadogTracer());
            traceContext.AppSecRequestContext.DisposeAdditiveContext();
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
            var span = new Span(spanContext, DateTimeOffset.Now);
            var initResult = CreateWaf();
            var waf = initResult.Waf;
            waf.Should().NotBeNull();
            var security = new AppSec.Security(waf: waf);
            var securityCoordinator = TryGet(security, span);
            var result = securityCoordinator.Value.RunWaf(new Dictionary<string, object> { { AddressesConstants.RequestMethod, "GET" } }, runWithEphemeral: true, isRasp: true);
            result.Should().BeNull();
        }

        [Fact]
        public void GivenHttpTransportInstanceWithUninitializedContext_WhenGetItems_ThenItemsIsNull()
        {
            var contextMoq = new Mock<HttpContext>();
            contextMoq.Setup(x => x.Items).Throws(new NullReferenceException("Test exception"));
            var context = contextMoq.Object;
            HttpTransport transport = new(context);
            transport.ReportedExternalWafsRequestHeaders.Should().BeFalse();
        }

        [Fact]
        public void GivenHttpTransportInstanceWithUninitializedContext_WhenRunWaf_ThenResultIsNull()
        {
            var settings = TracerSettings.Create(new Dictionary<string, object>());
            var tracer = new Tracer(settings, null, null, null, null);
            var rootTestScope = (Scope)tracer.StartActive("test.trace");

            var contextMoq = new Mock<HttpContext>();
            contextMoq.Setup(x => x.Items).Throws(new NullReferenceException("Test exception"));
            var context = contextMoq.Object;
            CoreHttpContextStore.Instance.Set(context);

            var securityCoordinator = TryGet(AppSec.Security.Instance, rootTestScope.Span);
            securityCoordinator.HasValue.Should().BeTrue();

            var result = new Result(new DdwafResultStruct(), WafReturnCode.Match, 0, 0);
            securityCoordinator.Value.Reporter.TryReport(result, true);

            rootTestScope.Span.Tags.GetTag(Tags.AppSecBlocked).Should().Be("true");
        }

        [Fact]
        public void GivenHttpTransportInstanceWithUninitializedContext_WhenAccessingStatusCode_ThenResultIsNull()
        {
            var settings = TracerSettings.Create(new Dictionary<string, object>());
            var tracer = new Tracer(settings, null, null, null, null);
            var rootTestScope = (Scope)tracer.StartActive("test.trace");

            var wafContext = new Mock<IContext>();

            var mockedFeatures = new Mock<IFeatureCollection>();
            mockedFeatures.Setup(x => x.Get<IContext>()).Returns(wafContext.Object);

            var contextMoq = new Mock<HttpContext>();
            contextMoq.Setup(x => x.Response.StatusCode).Throws(new NullReferenceException("Test exception"));
            contextMoq.Setup(x => x.Features).Returns(mockedFeatures.Object);

            var securityCoordinator = SecurityCoordinator.Get(AppSec.Security.Instance, rootTestScope.Span, new HttpTransport(contextMoq.Object));
            var result = securityCoordinator.RunWaf(new(), runWithEphemeral: true, isRasp: true);
            result.Should().BeNull();
        }

        [Fact]
        public void GivenSecurityCoordinatorInstanceWithNotDisposedContext_WheRunWaf_ThenResultIsNull()
        {
            var httpContextMock = new Mock<HttpContext>();
            var httpTransportMock = new Mock<HttpTransport>(httpContextMock.Object);
            httpTransportMock.Setup(x => x.StatusCode).Returns(200);
            httpTransportMock.Setup(x => x.RouteData).Returns(new Dictionary<string, object>());
            var traceContext = new TraceContext(new EmptyDatadogTracer());
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
            var span = new Span(spanContext, DateTimeOffset.Now);
            var initResult = CreateWaf();
            var waf = initResult.Waf;
            waf.Should().NotBeNull();
            var security = new AppSec.Security(waf: waf);
            var securityCoordinator = Get(security, span, httpTransportMock.Object);
            var result = securityCoordinator.RunWaf(new Dictionary<string, object> { { AddressesConstants.RequestMethod, "GET" } }, runWithEphemeral: true, isRasp: true);
            result.Should().NotBeNull();
        }
#endif

#if NETFRAMEWORK
        [Fact]
        public void GivenSecurityCoordinatorInstanceWithResponseHeadersWritten_WheBlock_ThenBlockExceptionAndNoError()
        {
            var traceContext = new TraceContext(new EmptyDatadogTracer());
            var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
            var span = new Span(spanContext, new DateTimeOffset());
            TextWriter textWriter = new StringWriter();
            var response = new HttpResponse(textWriter)
            {
                StatusCode = 200
            };

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
                securityCoordinator?.ReportAndBlock(resultMock.Object, () => Console.WriteLine("Telemtry reported"));
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
