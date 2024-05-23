// <copyright file="DiagnosticsUploadApiTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger;

public class DiagnosticsUploadApiTests
{
    private readonly TestRequestFactory _testRequestFactory;
    private readonly DiscoveryServiceMock _discoveryServiceMock;
    private readonly NullGitMetadataProvider _nullGitMetadataProvider;
    private readonly ArraySegment<byte> _data;

    public DiagnosticsUploadApiTests()
    {
        _testRequestFactory = new TestRequestFactory();
        _discoveryServiceMock = new DiscoveryServiceMock();
        _nullGitMetadataProvider = new NullGitMetadataProvider();
        _data = new ArraySegment<byte>(Encoding.UTF8.GetBytes("str"));
    }

    [Fact]
    public async Task DiagnosticsLegacy_RequestSentAsJson()
    {
        var api = DiagnosticsUploadApi.Create(_testRequestFactory, _discoveryServiceMock, _nullGitMetadataProvider);
        _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: "debugger/v1/input");

        await api.SendBatchAsync(_data);
        _testRequestFactory.RequestsSent.First().ContentType.Should().StartWith("application/json");
    }

    [Fact]
    public async Task DiagnosticsUpToDat_RequestSentAsMultipart()
    {
        var api = DiagnosticsUploadApi.Create(_testRequestFactory, _discoveryServiceMock, _nullGitMetadataProvider);
        _discoveryServiceMock.TriggerChange(diagnosticsEndpoint: "debugger/v1/diagnostics");

        await api.SendBatchAsync(_data);
        _testRequestFactory.RequestsSent.First().ContentType.Should().StartWith("multipart/form-data");
    }
}
