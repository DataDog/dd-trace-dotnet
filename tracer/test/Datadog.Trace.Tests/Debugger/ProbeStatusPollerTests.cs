// <copyright file="ProbeStatusPollerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using VerifyXunit;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    [UsesVerify]
    public class ProbeStatusPollerTests
    {
        private const string ServiceName = "test-service";
        private readonly TestDiagnosticsSink _sink;
        private readonly ProbeStatusPoller _poller;

        public ProbeStatusPollerTests()
        {
            _sink = new TestDiagnosticsSink(ServiceName);
            _poller = CreatePoller(_sink);
        }

        [Fact]
        public async Task WhenDisposed_NoNewProbesAccepted()
        {
            var probe = CreateProbe("test-probe-1");
            _poller.Dispose();
            _poller.AddProbes(new[] { probe });
            await Task.Delay(2000);
            Assert.Empty(_sink.GetDiagnostics());
        }

        [Fact]
        public async Task WhenDisposedDuringPolling_CompletesGracefully()
        {
            var probe = CreateProbe("test-probe-2");
            var disposalCompleted = new TaskCompletionSource<bool>();
            _poller.StartPolling();
            _poller.AddProbes(new[] { probe });
            await Task.Run(() =>
            {
                Thread.Sleep(100);
                _poller.Dispose();
                disposalCompleted.SetResult(true);
            });

            var disposalTask = await Task.WhenAny(disposalCompleted.Task, Task.Delay(5000));
            Assert.Same(disposalTask, disposalCompleted.Task);
        }

        [Fact]
        public async Task WhenProbeStatusChanges_ShouldUpdateDiagnostics()
        {
            var probeId = "test-probe-3";
            var probe = CreateProbe(probeId);
            _poller.StartPolling();
            _poller.AddProbes(new[] { probe });
            await Task.Delay(1000);
            var diagnostics = _sink.GetDiagnostics();
            Assert.Contains(diagnostics, d => d.DebuggerDiagnostics.Diagnostics.ProbeId == probeId);
            _sink.AddProbeStatus(probeId, Status.EMITTING, 1);
            await Task.Delay(1000);
            diagnostics = _sink.GetDiagnostics();
            var updatedProbe = diagnostics.Find(d => d.DebuggerDiagnostics.Diagnostics.ProbeId == probeId);
            Assert.NotNull(updatedProbe);
            Assert.Equal(Status.EMITTING, updatedProbe.DebuggerDiagnostics.Diagnostics.Status);
        }

        [Fact]
        public async Task WhenProbeRemoved_ShouldNotAppearInDiagnostics()
        {
            var probeId = "test-probe-4";
            var probe = CreateProbe(probeId);
            _poller.StartPolling();
            _poller.AddProbes(new[] { probe });
            await Task.Delay(1000);
            _poller.RemoveProbes(new[] { probeId });
            await Task.Delay(1000);
            var diagnostics = _sink.GetDiagnostics();
            Assert.DoesNotContain(diagnostics, d => d.DebuggerDiagnostics.Diagnostics.ProbeId == probeId);
        }

        private static ProbeStatusPoller CreatePoller(DiagnosticsSink sink)
        {
            var settings = new DebuggerSettings(
                new NameValueConfigurationSource(new NameValueCollection()),
                NullConfigurationTelemetry.Instance);
            return ProbeStatusPoller.Create(sink, settings);
        }

        private static FetchProbeStatus CreateProbe(string id, int version = 1)
        {
            return new FetchProbeStatus(id, version);
        }

        private class TestDiagnosticsSink : DiagnosticsSink
        {
            public TestDiagnosticsSink(string serviceName)
                : base(serviceName, batchSize: 100, interval: TimeSpan.FromSeconds(1))
            {
            }

            public new List<ProbeStatus> GetDiagnostics()
            {
                return base.GetDiagnostics();
            }
        }
    }
}
