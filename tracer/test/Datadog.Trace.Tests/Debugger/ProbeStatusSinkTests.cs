// <copyright file="ProbeStatusSinkTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Linq;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.ProbeStatuses;
using Datadog.Trace.Debugger.Sink;
using Datadog.Trace.Debugger.Sink.Models;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;
using NativeProbeStatus = Datadog.Trace.Debugger.PInvoke.ProbeStatus;

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
            _sink = DiagnosticsSink.Create(() => "ProbeStatusSinkTests", _settings);
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
        public void RemovePreservesQueuedEmitting()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING)
            });
        }

        [Fact]
        public void RemoveDropsQueuedStatusBeforePreservedEmitting()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.INSTALLED);
            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING)
            });
        }

        [Fact]
        public void RemovedEmittingDoesNotReemitOnInterval()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);
            _sink.GetDiagnostics().Should().NotBeEmpty();
            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));

            _sink.GetDiagnostics().Should().BeEmpty();
        }

        [Fact]
        public void RemovedEmittingClearsGenerationAfterEmit()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);
            _sink.GetDiagnostics().Should().NotBeEmpty();

            _sink.HasGeneration(probeId).Should().BeFalse();
        }

        [Fact]
        public void RemovingDrainedEmittingDoesNotReemitOnInterval()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.GetDiagnostics().Should().NotBeEmpty();
            _sink.Remove(probeId);
            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));

            _sink.GetDiagnostics().Should().BeEmpty();
        }

        [Fact]
        public void RemoveDropsQueuedEmittingIfLatestStatusIsError()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.AddProbeStatus(probeId, Status.ERROR, errorMessage: "error");
            _sink.Remove(probeId);

            _sink.GetDiagnostics().Should().BeEmpty();
        }

        [Fact]
        public void RemoveDropsQueuedEmittingIfLaterStatusIsError()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);
            _sink.AddProbeStatus(probeId, Status.ERROR, errorMessage: "error");

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.ERROR, errorMessage: "error")
            });
        }

        [Fact]
        public void RemovePreservesLaterEmittingAsCurrentStatus()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);
            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING)
            });

            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING)
            });
        }

        [Fact]
        public void RemovePreservesEmittingUnderQueuePressure()
        {
            var probeId = "emitting-probe-" + Guid.NewGuid();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);
            for (var i = 1; i <= 1000; i++)
            {
                _sink.AddProbeStatus($"error-probe-{i}", Status.ERROR, errorMessage: i.ToString());
            }

            _sink.GetDiagnostics().Should().Contain(new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING));
        }

        [Fact]
        public void RemovePreservesEmittingAfterQueueRecreationFromRepeatedUpdates()
        {
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);
            for (var i = 0; i < 1001; i++)
            {
                _sink.AddProbeStatus("error-probe", Status.ERROR, errorMessage: i.ToString());
            }

            _sink.GetDiagnostics().Should().Contain(new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING));
        }

        [Fact]
        public void RemovePreservesEmittingAddedWhenQueueIsAlreadyFull()
        {
            var probeId = Guid.NewGuid().ToString();
            for (var i = 0; i < 1000; i++)
            {
                _sink.AddProbeStatus(i.ToString(), Status.ERROR, errorMessage: i.ToString());
            }

            _sink.AddProbeStatus(probeId, Status.EMITTING);
            _sink.Remove(probeId);

            _sink.GetDiagnostics().Should().Contain(new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING));
        }

        [Fact]
        public void RemoveDropsPreservedEmittingOverflowAfterQueueRecreation()
        {
            const string errorProbeId = "error-probe";
            for (var i = 0; i < 1001; i++)
            {
                var probeId = $"emitting-probe-{i}";
                _sink.AddProbeStatus(probeId, Status.EMITTING);
                _sink.Remove(probeId);
            }

            _sink.AddProbeStatus(errorProbeId, Status.ERROR, errorMessage: "error");
            var maxDrainAttempts = (1002 / _settings.UploadBatchSize * 2) + 1;
            var drainAttempts = 0;
            while (_sink.GetDiagnostics().Count != 0 && drainAttempts < maxDrainAttempts)
            {
                drainAttempts++;
            }

            drainAttempts.Should().BeLessThan(maxDrainAttempts);
            for (var i = 0; i < 1001; i++)
            {
                _sink.HasGeneration($"emitting-probe-{i}").Should().BeFalse();
            }

            _sink.HasGeneration(errorProbeId).Should().BeTrue();
        }

        [Fact]
        public void RecreateQueueDoesNotDelayNormalStatusThatDoesNotFit()
        {
            _timeLord.StopTime();
            const string normalProbeId = "normal-probe";

            _sink.AddProbeStatus(normalProbeId, Status.RECEIVED);
            _sink.GetDiagnostics().Should().NotBeEmpty();
            for (var i = 0; i < 1000; i++)
            {
                var probeId = $"emitting-probe-{i}";
                _sink.AddProbeStatus(probeId, Status.EMITTING);
                _sink.Remove(probeId);
            }

            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));
            _sink.AddProbeStatus("priority-emitting-probe", Status.EMITTING);

            var maxDrainAttempts = (1000 / _settings.UploadBatchSize) + 2;
            var returnedNormalStatus = false;
            for (var i = 0; i < maxDrainAttempts && !returnedNormalStatus; i++)
            {
                returnedNormalStatus = _sink.GetDiagnostics().Any(status => status.DebuggerDiagnostics.Diagnostics.ProbeId == normalProbeId);
            }

            returnedNormalStatus.Should().BeTrue();
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
        public void GetDiagnosticsReturnsAtMostUploadBatchSize()
        {
            for (var i = 0; i <= _settings.UploadBatchSize; i++)
            {
                var probeId = $"probe-{i}";
                _sink.AddProbeStatus(probeId, Status.RECEIVED);
            }

            _sink.GetDiagnostics().Should().HaveCount(_settings.UploadBatchSize);
        }

        [Fact]
        public void DoNotReemitWhenStillQueuedAfterInterval()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();

            _sink.AddProbeStatus(probeId, Status.RECEIVED);
            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.RECEIVED)
            });
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

        [Fact]
        public void PollerEmitsNonDefaultStatusBeforeDelayedPoll()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var fetchProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.EMITTING));

            poller.UpdateProbe(probeId, fetchProbeStatus);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING, probeVersion: 1)
            });
        }

        [Fact]
        public void PollerPreservesEmittingDiagnosticWhenProbeIsRemovedBeforeDelayedPoll()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var fetchProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.EMITTING));

            poller.UpdateProbe(probeId, fetchProbeStatus);
            poller.RemoveProbes([probeId]);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING, probeVersion: 1)
            });
        }

        [Fact]
        public void PollerDoesNotDoubleEmitRepeatedNonDefaultStatus()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var fetchProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.EMITTING));

            poller.UpdateProbe(probeId, fetchProbeStatus);
            poller.UpdateProbe(probeId, fetchProbeStatus);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING, probeVersion: 1)
            });
        }

        [Fact]
        public void PollerDropsQueuedExplicitStatusWhenUpdatingToNewExplicitVersion()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var oldInstalledProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.INSTALLED));
            var newErrorProbeStatus = new FetchProbeStatus(probeId, 2, new NativeProbeStatus(probeId, Status.ERROR, "error"));

            poller.UpdateProbe(probeId, oldInstalledProbeStatus);
            poller.UpdateProbe(probeId, newErrorProbeStatus);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.ERROR, probeVersion: 2, errorMessage: "error")
            });
        }

        [Fact]
        public void PollerDropsQueuedEmittingWhenUpdatingToNewExplicitVersion()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var oldEmittingProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.EMITTING));
            var newReceivedProbeStatus = new FetchProbeStatus(probeId, 2, new NativeProbeStatus(probeId, Status.RECEIVED));

            poller.UpdateProbe(probeId, oldEmittingProbeStatus);
            poller.UpdateProbe(probeId, newReceivedProbeStatus);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.RECEIVED, probeVersion: 2)
            });
        }

        [Fact]
        public void PollerClearsDiagnosticsWhenUpdatingToDefaultStatus()
        {
            _timeLord.StopTime();
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var installedProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.INSTALLED));
            var defaultProbeStatus = new FetchProbeStatus(probeId, 1);

            poller.UpdateProbe(probeId, installedProbeStatus);
            _sink.GetDiagnostics().Should().NotBeEmpty();
            poller.UpdateProbe(probeId, defaultProbeStatus);
            _timeLord.TravelTo(_timeLord.UtcNow.AddSeconds(_settings.DiagnosticsIntervalSeconds));

            _sink.GetDiagnostics().Should().BeEmpty();
        }

        [Fact]
        public void PollerClearsEmittingDiagnosticsWhenUpdatingToDefaultStatus()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var emittingProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.EMITTING));
            var defaultProbeStatus = new FetchProbeStatus(probeId, 1);

            poller.UpdateProbe(probeId, emittingProbeStatus);
            poller.UpdateProbe(probeId, defaultProbeStatus);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.EMITTING, probeVersion: 1)
            });
        }

        [Fact]
        public void PollerDropsQueuedEmittingFromBeforeDefaultUpdateWhenProbeIsReaddedWithExplicitStatus()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var oldEmittingProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.EMITTING));
            var defaultProbeStatus = new FetchProbeStatus(probeId, 1);
            var newInstalledProbeStatus = new FetchProbeStatus(probeId, 2, new NativeProbeStatus(probeId, Status.INSTALLED));

            poller.UpdateProbe(probeId, oldEmittingProbeStatus);
            poller.UpdateProbe(probeId, defaultProbeStatus);
            poller.UpdateProbe(probeId, newInstalledProbeStatus);

            _sink.GetDiagnostics().Should().Equal(new[]
            {
                new ProbeStatus(nameof(ProbeStatusSinkTests), probeId, Status.INSTALLED, probeVersion: 2)
            });
        }

        [Fact]
        public void PollerClearsQueuedInstalledStatusGenerationAfterDefaultUpdate()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var oldInstalledProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.INSTALLED));
            var defaultProbeStatus = new FetchProbeStatus(probeId, 1);

            poller.UpdateProbe(probeId, oldInstalledProbeStatus);
            poller.UpdateProbe(probeId, defaultProbeStatus);
            _sink.GetDiagnostics().Should().BeEmpty();

            _sink.HasGeneration(probeId).Should().BeFalse();
        }

        [Fact]
        public void PollerDropsQueuedInstalledStatusGenerationWhenQueueIsRecreatedBeforeDrain()
        {
            var probeId = Guid.NewGuid().ToString();
            using var poller = ProbeStatusPoller.Create(_sink, _settings);
            var oldInstalledProbeStatus = new FetchProbeStatus(probeId, 1, new NativeProbeStatus(probeId, Status.INSTALLED));
            var defaultProbeStatus = new FetchProbeStatus(probeId, 1);

            poller.UpdateProbe(probeId, oldInstalledProbeStatus);
            poller.UpdateProbe(probeId, defaultProbeStatus);
            for (var i = 0; i < 1001; i++)
            {
                _sink.AddProbeStatus("error-probe", Status.ERROR, errorMessage: i.ToString());
            }

            _sink.GetDiagnostics().Should().NotContain(status => status.DebuggerDiagnostics.Diagnostics.ProbeId == probeId);

            _sink.HasGeneration(probeId).Should().BeFalse();
        }
    }
}
