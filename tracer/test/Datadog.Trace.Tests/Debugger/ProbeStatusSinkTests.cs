// <copyright file="ProbeStatusSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Debugger
{
    public class ProbeStatusSinkTests
    {
        private readonly TimeLord _timeLord;
        private readonly DiagnosticsSink _sink;
        private readonly DebuggerSettings _settings;

        public ProbeStatusSinkTests()
        {
            _timeLord = new TimeLord();
            Clock.SetForCurrentThread(_timeLord);

            _settings = DebuggerSettings.FromDefaultSource();
            _sink = DiagnosticsSink.Create(nameof(ProbeStatusSinkTests), _settings);
        }

        [Fact]
        public void AddReceived()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);

            var probes = _sink.GetDiagnostics();
            probes.Count.Should().Be(1);

            var probe = probes.First();
            probe.DebuggerDiagnostics.Diagnostics.Status.Should().Be(Status.RECEIVED);
            probe.DebuggerDiagnostics.Diagnostics.ProbeId.Should().Be(probeId);
            probe.Service.Should().Be(nameof(ProbeStatusSinkTests));
            probe.Message.Should().Be($"Received probe {probeId}.");
            probe.DebuggerDiagnostics.Diagnostics.Exception.Should().BeNull();
        }

        [Fact]
        public void AddInstalled()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.INSTALLED);

            var probes = _sink.GetDiagnostics();
            probes.Count.Should().Be(1);

            var probe = probes.First();
            probe.DebuggerDiagnostics.Diagnostics.Status.Should().Be(Status.INSTALLED);
            probe.DebuggerDiagnostics.Diagnostics.ProbeId.Should().Be(probeId);
            probe.Service.Should().Be(nameof(ProbeStatusSinkTests));
            probe.Message.Should().Be($"Installed probe {probeId}.");
            probe.DebuggerDiagnostics.Diagnostics.Exception.Should().BeNull();
        }

        [Fact]
        public void AddBlocked()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.BLOCKED);

            var probes = _sink.GetDiagnostics();
            probes.Count.Should().Be(1);

            var probe = probes.First();
            probe.DebuggerDiagnostics.Diagnostics.Status.Should().Be(Status.BLOCKED);
            probe.DebuggerDiagnostics.Diagnostics.ProbeId.Should().Be(probeId);
            probe.Service.Should().Be(nameof(ProbeStatusSinkTests));
            probe.Message.Should().Be($"Blocked probe {probeId}.");
            probe.DebuggerDiagnostics.Diagnostics.Exception.Should().BeNull();
        }

        [Fact]
        public void AddError()
        {
            var probeId = Guid.NewGuid().ToString();
            var exception = new InvalidOperationException($"Test exceptions at ${nameof(AddError)}");

            _sink.AddProbeStatus(probeId, Status.ERROR, exception: exception);

            var probes = _sink.GetDiagnostics();
            probes.Count.Should().Be(1);

            var probe = probes.First();
            probe.DebuggerDiagnostics.Diagnostics.Status.Should().Be(Status.ERROR);
            probe.DebuggerDiagnostics.Diagnostics.ProbeId.Should().Be(probeId);
            probe.Service.Should().Be(nameof(ProbeStatusSinkTests));
            probe.Message.Should().Be($"Error installing probe {probeId}.");
            probe.DebuggerDiagnostics.Diagnostics.Exception.Should().NotBeNull();
            probe.DebuggerDiagnostics.Diagnostics.Exception.Type.Should().Be(exception.GetType().Name);
            probe.DebuggerDiagnostics.Diagnostics.Exception.Message.Should().Be(exception.Message);
            probe.DebuggerDiagnostics.Diagnostics.Exception.StackTrace.Should().Be(exception.StackTrace);
        }

        [Fact]
        public void AddReceivedThenInstalled()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _sink.AddProbeStatus(probeId, Status.INSTALLED);

            var probes = _sink.GetDiagnostics();
            probes.Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.RECEIVED),
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.INSTALLED)
            });
        }

        [Fact]
        public void AddReceivedThenBlocked()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _sink.AddProbeStatus(probeId, Status.BLOCKED);

            var probes = _sink.GetDiagnostics();
            probes.Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.RECEIVED),
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.BLOCKED)
            });
        }

        [Fact]
        public void AddReceivedThenError()
        {
            var probeId = Guid.NewGuid().ToString();
            var exception = new InvalidOperationException($"Test exceptions at ${nameof(AddError)}");

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _sink.AddProbeStatus(probeId, Status.ERROR, exception: exception);

            var probes = _sink.GetDiagnostics();
            probes.Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.RECEIVED),
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.ERROR, exception: exception)
            });
        }

        [Fact]
        public void AddReceivedThenInstalledThenError()
        {
            var probeId = Guid.NewGuid().ToString();
            var exception = new InvalidOperationException($"Test exceptions at ${nameof(AddError)}");

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _sink.AddProbeStatus(probeId, Status.INSTALLED);
            _sink.AddProbeStatus(probeId, Status.ERROR, exception: exception);

            var probes = _sink.GetDiagnostics();
            probes.Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.RECEIVED),
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.INSTALLED),
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.ERROR, exception: exception)
            });
        }

        [Fact]
        public void Remove()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _sink.Remove(probeId);

            var probes = _sink.GetDiagnostics();
            probes.Should().BeEmpty();
        }

        [Fact]
        public void DoNotDoubleEmitMessageIfIntervalHasntPassed()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);

            _sink.GetDiagnostics().Should().NotBeEmpty();
            _sink.GetDiagnostics().Should().BeEmpty();
        }

        [Fact]
        public void ReemitOnInterval()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);

            _sink.GetDiagnostics().Should().NotBeEmpty();
            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));
            _sink.GetDiagnostics().Should().NotBeEmpty();
        }

        [Fact]
        public void ReemitOnlyLatestMessage()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();
            var exception = new InvalidOperationException($"Custom exception for {nameof(ReemitOnlyLatestMessage)}");

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _sink.AddProbeStatus(probeId, Status.ERROR, exception: exception);
            _sink.GetDiagnostics().Should().NotBeEmpty();

            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));
            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.ERROR, exception: exception)
            });
        }

        [Fact]
        public void DropDiagnostics()
        {
            var probeId = Guid.NewGuid().ToString();
            _sink.AddProbeStatus(probeId, Status.RECEIVED);

            var exception = new InvalidOperationException($"Custom exception for {nameof(ReemitOnlyLatestMessage)}");
            for (var i = 0; i < 999; i++)
            {
                _sink.AddProbeStatus(probeId, Status.ERROR, exception: exception);
            }

            _sink.AddProbeStatus(probeId, Status.ERROR, exception: exception);
            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.ERROR, exception: exception)
            });
        }
    }
}
