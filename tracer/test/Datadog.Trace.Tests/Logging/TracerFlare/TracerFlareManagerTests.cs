// <copyright file="TracerFlareManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Logging.TracerFlare;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers.TransportHelpers;
using Datadog.Trace.Tests.Agent;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Logging.TracerFlare;

public class TracerFlareManagerTests
{
    private const string ConfigPath = "/some/path";
    private readonly string _requestId = Guid.NewGuid().ToString();
    private readonly string _logsDir;
    private readonly byte[] _validConfigBytes = Encoding.UTF8.GetBytes("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123","hostname": "my.hostname", "user_handle": "its.me@datadoghq.com" } }""");

    public TracerFlareManagerTests()
    {
        // Ensure we have a valid log directory
        _logsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_logsDir);
    }

    [Fact]
    public void CanSendFlare_AfterDiscovery_IsTrue()
    {
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TracerFlareManager(
            discoveryService,
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(Mock.Of<IApiRequestFactory>()));

        manager.Start();

        manager.CanSendTracerFlare.Should().BeNull();
        discoveryService.TriggerChange();

        manager.CanSendTracerFlare.Should().BeTrue();
    }

    [Fact]
    public void CanSendFlare_AfterDiscoveryFails_IsFalse()
    {
        var discoveryService = new DiscoveryServiceMock();
        var manager = new TracerFlareManager(
            discoveryService,
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(Mock.Of<IApiRequestFactory>()));

        manager.Start();

        manager.CanSendTracerFlare.Should().BeNull();
        discoveryService.TriggerChange(tracerFlareEndpoint: null);

        manager.CanSendTracerFlare.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{ "task_type": "tracer_flare"}""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": {} }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": null} }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": 123} }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "" } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": null } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": "" } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": 123 } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": "my.host", "user_handle": null } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": "my.host", "user_handle": "" } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": "my.host", "user_handle": 123 } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": "abc123", "hostname": null, "user_handle": "test@datadog.com" } }""")]
    [InlineData("""{ "task_type": "tracer_flare", "args": { "case_id": null, "hostname": "my.host", "user_handle": "test@datadog.com" } }""")]
    public async Task InvalidConfig_DoesNotSend_ReturnsError(string config)
    {
        var requestMock = new Mock<IApiRequestFactory>();
        var manager = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(requestMock.Object));

        var result = await manager.TrySendDebugLogs(ConfigPath, Encoding.UTF8.GetBytes(config), _requestId, _logsDir);

        result.Filename.Should().Be(ConfigPath);
        result.ApplyState.Should().Be(ApplyStates.ERROR);
        result.Error.Should().NotBeNull();
        requestMock.Verify(x => x.Create(It.IsAny<Uri>()), Times.Never);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "task_type": "agent_flare"}""")]
    [InlineData("""{ "something": "else"}""")]
    public async Task ValidNonApplicableConfig_DoesNotSend_ReturnsSuccess(string config)
    {
        var requestMock = new Mock<IApiRequestFactory>();
        var manager = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(requestMock.Object));

        var result = await manager.TrySendDebugLogs(ConfigPath, Encoding.UTF8.GetBytes(config), _requestId, _logsDir);

        result.Filename.Should().Be(ConfigPath);
        result.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);
        result.Error.Should().BeNull();
        requestMock.Verify(x => x.Create(It.IsAny<Uri>()), Times.Never);
    }

    [Fact]
    public async Task WhenClaimCannotBeCreatedOrRead_DoesNotSend_ReportsUnknownOutcome()
    {
        var requestMock = new Mock<IApiRequestFactory>();
        var manager = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(requestMock.Object));

        var result = await manager.TrySendDebugLogs(ConfigPath, _validConfigBytes, _requestId, Path.Combine(_logsDir, "missing"));

        result.Filename.Should().Be(ConfigPath);
        result.ApplyState.Should().Be(ApplyStates.ERROR);
        result.Error.Should().Be("The debug log upload outcome is unavailable");
        requestMock.Verify(x => x.Create(It.IsAny<Uri>()), Times.Never);
    }

    [Fact]
    public async Task WhenLegacySentinelAndAbandonedReservationExist_AcknowledgesImmediately()
    {
        var claimPath = Path.Combine(_logsDir, $"dotnet-tracer-sentinel-{_requestId}.log");
        File.WriteAllBytes(claimPath + ".pending", [3]);
        File.WriteAllBytes(claimPath, []);
        var requests = new TestRequestFactory();
        var manager = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(requests));

        var upload = manager.TrySendDebugLogs(ConfigPath, _validConfigBytes, _requestId, _logsDir);

        upload.IsCompleted.Should().BeTrue();
        var result = await upload;
        result.Filename.Should().Be(ConfigPath);
        result.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);
        requests.RequestsSent.Should().BeEmpty();
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task WhenFirstUploadFailsDuringReplay_BothCallersReportTheError(bool directoryClaim)
    {
        Skip.If(directoryClaim && !LinuxFlareClaimDirectory.IsSupported, "Directory claims require Linux x64 or ARM64.");
        if (directoryClaim)
        {
            Directory.CreateDirectory(Path.Combine(_logsDir, $"dotnet-tracer-sentinel-{_requestId}.log"));
        }

        var uploadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finishUpload = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failedRequests = new TestRequestFactory(
            endpoint => new HeldUploadRequest(endpoint, uploadStarted, finishUpload.Task));
        var original = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(failedRequests));
        var firstUpload = original.TrySendDebugLogs(ConfigPath, _validConfigBytes, _requestId, _logsDir);
        (await Task.WhenAny(uploadStarted.Task, firstUpload)).Should().BeSameAs(uploadStarted.Task);

        var replayRequests = new TestRequestFactory();
        var successor = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(replayRequests));
        var waiting = successor.TrySendDebugLogs(ConfigPath, _validConfigBytes, _requestId, _logsDir);
        var completedBeforeUpload = waiting.IsCompleted;
        finishUpload.SetResult(true);
        var outcomes = await Task.WhenAll(firstUpload, waiting);

        completedBeforeUpload.Should().BeFalse();
        outcomes[0].ApplyState.Should().Be(ApplyStates.ERROR);
        outcomes[0].Error.Should().Contain("mock upload failed");
        outcomes[1].ApplyState.Should().Be(ApplyStates.ERROR);
        outcomes[1].Error.Should().Be("There was an error uploading the debug log archive");
        failedRequests.RequestsSent.Should().ContainSingle();
        replayRequests.RequestsSent.Should().BeEmpty();
    }

    [SkippableTheory]
    [CombinatorialData]
    public async Task WhenShouldShip_SendsRequest_ReturnsSuccess(bool directoryClaim)
    {
        Skip.If(directoryClaim && !LinuxFlareClaimDirectory.IsSupported, "Directory claims require Linux x64 or ARM64.");
        var requestFactory = new TestRequestFactory();
        var manager = new TracerFlareManager(
            new DiscoveryServiceMock(),
            Mock.Of<IRcmSubscriptionManager>(),
            NullTelemetryController.Instance,
            new TracerFlareApi(requestFactory));

        // Create a new sentinel to make sure there's no flake
        var configId = Guid.NewGuid().ToString();
        if (directoryClaim)
        {
            Directory.CreateDirectory(Path.Combine(_logsDir, $"dotnet-tracer-sentinel-{configId}.log"));
        }

        var result = await manager.TrySendDebugLogs(ConfigPath, _validConfigBytes, configId, _logsDir);

        result.Filename.Should().Be(ConfigPath);
        result.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);
        result.Error.Should().BeNull();

        var replay = await manager.TrySendDebugLogs(ConfigPath, _validConfigBytes, configId, _logsDir);
        replay.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);
        requestFactory.RequestsSent.Should().ContainSingle();
    }

    private sealed class HeldUploadRequest(Uri endpoint, TaskCompletionSource<bool> started, Task finishUpload)
        : TestApiRequest(endpoint, statusCode: 500, responseContent: """{"error":"mock upload failed"}""")
    {
        public override async Task<IApiResponse> PostAsync(ArraySegment<byte> bytes, string contentType, string contentEncoding)
        {
            started.SetResult(true);
            await finishUpload.ConfigureAwait(false);
            return await base.PostAsync(bytes, contentType, contentEncoding).ConfigureAwait(false);
        }
    }
}
