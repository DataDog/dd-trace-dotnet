// <copyright file="OneShotTelemetryReporterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Telemetry;
using Xunit;

namespace Datadog.Trace.Tools.Runner.Tests;

public class OneShotTelemetryReporterTests
{
    [Theory]
    [InlineData(TelemetryPushResult.Success, TelemetryPushResult.TransientFailure)]
    [InlineData(TelemetryPushResult.TransientFailure, TelemetryPushResult.FatalError, TelemetryPushResult.Success)]
    internal async Task IfAtLeastOneTransportSucceedsOnFirstAttempt_ReportSuccess(params TelemetryPushResult[] results)
    {
        var reporter = new OneShotTelemetryReporter(results.Select(r => new TestTransport(r)).ToArray());
        var result = await reporter.UploadAsLogMessage("hi", GetApplicationData());
        Assert.True(result);
    }

    [Theory]
    [InlineData(TelemetryPushResult.TransientFailure)]
    [InlineData(TelemetryPushResult.TransientFailure, TelemetryPushResult.FatalError, TelemetryPushResult.TransientFailure)]
    internal async Task IfAllTransportsFailOnFirstAttempt_ReportFailure(params TelemetryPushResult[] results)
    {
        var reporter = new OneShotTelemetryReporter(results.Select(r => new TestTransport(r)).ToArray());
        var result = await reporter.UploadAsLogMessage("hi", GetApplicationData());
        Assert.False(result);
    }

    [Fact]
    internal async Task ShouldStopAfterSuccess()
    {
        var transport1 = new TestTransport(TelemetryPushResult.Success);
        var transport2 = new TestTransport(TelemetryPushResult.Success);
        var reporter = new OneShotTelemetryReporter(new[] { transport1, transport2 });
        var result = await reporter.UploadAsLogMessage("hi", GetApplicationData());

        Assert.True(result);
        Assert.True(transport1.WasUsed);
        Assert.False(transport2.WasUsed);
    }

    private static ApplicationTelemetryData GetApplicationData()
    {
        return new ApplicationTelemetryData(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private class TestTransport : ITelemetryTransport
    {
        private readonly TelemetryPushResult[] _results;
        private int _current = -1;

        public TestTransport(params TelemetryPushResult[] results)
        {
            _results = results;
        }

        public bool WasUsed => _current >= 0;

        public Task<TelemetryPushResult> PushTelemetry(TelemetryData data)
        {
            _current++;
            if (_current >= _results.Length)
            {
                throw new InvalidOperationException("Transport received unexpected request");
            }

            return Task.FromResult(_results[_current]);
        }

        public string GetTransportInfo()
        {
            return nameof(TestTransport);
        }
    }
}
