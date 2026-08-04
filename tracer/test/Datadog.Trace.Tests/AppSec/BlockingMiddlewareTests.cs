// <copyright file="BlockingMiddlewareTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AspNetCore;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.AppSec;

/// <summary>
/// ASP.NET Core pools <see cref="HttpContext"/> instances and uninitializes them (setting their feature
/// collection to null) once the request is over. Reading such a context throws a
/// <see cref="NullReferenceException"/> from inside ASP.NET Core itself, and the middleware has to survive
/// that instead of turning the customer's request into a 500.
/// </summary>
[Collection(nameof(TracerInstanceTestCollection))]
[TracerRestorer]
public class BlockingMiddlewareTests
{
    [Fact]
    public async Task GivenAnUninitializedHttpContext_WhenTheEndOfPipelineMiddlewareRuns_NoExceptionIsThrown()
    {
        using var securityRestorer = OverrideSecurityInstance(appsecEnabled: true);
        await using var tracer = SetUpTracer();
        using var scope = tracer.StartActive("root");

        var context = UninitializedHttpContext();

        // endPipeline is the instance registered after the endpoint middleware, the one that tweaks the
        // status code to 404 before running the discovery scan checks
        var middleware = new BlockingMiddleware(next: null, endPipeline: true);

        var invoke = () => middleware.Invoke(context);

        await invoke.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GivenAnUninitializedHttpContext_WhenTheRestOfThePipelineBlocks_NoExceptionIsThrown()
    {
        using var securityRestorer = OverrideSecurityInstance(appsecEnabled: false);
        await using var tracer = SetUpTracer();
        using var scope = tracer.StartActive("root");

        var context = UninitializedHttpContext();

        var blockException = new BlockException(Mock.Of<IResult>(), new Dictionary<string, object?>());
        var middleware = new BlockingMiddleware(_ => throw blockException);

        var invoke = () => middleware.Invoke(context);

        // we can't write the blocking response on an unreadable context, but the BlockException must not
        // escape into the customer's pipeline either. Note this doesn't observe the fail-closed choice in
        // CouldNotWriteBlockingResponse: the pipeline has already run by the time we get here, so the
        // return value is dead on this path.
        await invoke.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GivenAReadableHttpContext_WhenWritingTheBlockingResponseFails_TheExceptionIsNotSwallowed()
    {
        using var securityRestorer = OverrideSecurityInstance(appsecEnabled: false);
        await using var tracer = SetUpTracer();
        using var scope = tracer.StartActive("root");

        var context = new DefaultHttpContext();
        context.Response.Body = new ThrowingStream();

        var blockException = new BlockException(Mock.Of<IResult>(), new Dictionary<string, object?>());
        var middleware = new BlockingMiddleware(_ => throw blockException);

        var invoke = () => middleware.Invoke(context);

        // the context is perfectly readable here, so a failure while writing the body is a genuine error
        // (ours or the customer's) and must not be misreported as a recycled context and swallowed
        await invoke.Should().ThrowAsync<NullReferenceException>();
    }

    private static DefaultHttpContext UninitializedHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Uninitialize();

        // this is the failure the tracer has to survive: it comes from ASP.NET Core, not from our code
        var readTheRequest = () => context.Request.Cookies;
        readTheRequest.Should().Throw<NullReferenceException>();

        return context;
    }

    private static ScopedTracer SetUpTracer()
    {
        // the middleware reads Tracer.Instance.ActiveScope, so the test tracer has to be the global one
        var tracer = TracerHelper.CreateWithFakeAgent();
        TracerRestorerAttribute.SetTracer(tracer);
        return tracer;
    }

    private static SecurityRestorer OverrideSecurityInstance(bool appsecEnabled)
    {
        var previous = Security.Instance;
        var source = new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.AppSec.Enabled, "0" } });
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);

        // wafIsNull: false keeps Security from initializing the native WAF, which isn't available here. The
        // middleware never gets that far anyway: Scan gives up as soon as it can't read the request.
        var configurationState = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: false) { AppsecEnabled = appsecEnabled };

        Security.Instance = new Security(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>(), configurationState: configurationState);
        Security.Instance.AppsecEnabled.Should().Be(appsecEnabled);

        return new SecurityRestorer(previous);
    }

    private sealed class SecurityRestorer(Security previous) : IDisposable
    {
        public void Dispose() => Security.Instance = previous;
    }

    /// <summary>
    /// Stands in for a response body that fails while we're writing the blocking response, the way a
    /// customer-supplied response feature could.
    /// </summary>
    private sealed class ThrowingStream : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NullReferenceException();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => throw new NullReferenceException();

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => throw new NullReferenceException();
    }
}
#endif
