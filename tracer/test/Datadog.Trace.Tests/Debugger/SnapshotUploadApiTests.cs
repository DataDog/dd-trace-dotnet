// <copyright file="SnapshotUploadApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.TestHelpers.TransportHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class SnapshotUploadApiTests
    {
        [Fact]
        public async Task UsesStaticEndpointWhenProvided()
        {
            var factory = new TestRequestFactory(new Uri("https://debugger-intake.example/"));
            var gitMetadataProvider = new NullGitMetadataProvider();
            var snapshotApi = SnapshotUploadApi.Create(factory, discoveryService: null, gitMetadataProvider, staticEndpoint: "/api/v2/debugger");

            var payload = new ArraySegment<byte>(new byte[] { 1, 2, 3 });
            var success = await snapshotApi.SendBatchAsync(payload);

            success.Should().BeTrue();
            factory.RequestsSent.Should().ContainSingle();
            var requestUri = factory.RequestsSent[0].Endpoint;
            requestUri.GetLeftPart(UriPartial.Path).Should().Be("https://debugger-intake.example/api/v2/debugger");
            requestUri.Query.Should().Contain("ddtags=");
        }
    }
}
