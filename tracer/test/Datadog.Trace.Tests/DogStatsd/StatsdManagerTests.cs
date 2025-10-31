// <copyright file="StatsdManagerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.DogStatsd;
using Datadog.Trace.Vendors.StatsdClient;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.DogStatsd;

public class StatsdManagerTests
{
    private static readonly TracerSettings TracerSettings = new();
    private static readonly MutableSettings PreviousMutable = MutableSettings.CreateForTesting(TracerSettings, []);
    private static readonly ExporterSettings PreviousExporter = new ExporterSettings(null);

    [Fact]
    public void HasImpactingChanges_WhenNoChanges()
    {
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: null,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeFalse();
    }

    [Fact]
    public void HasImpactingChanges_WhenNoChanges2()
    {
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: PreviousMutable,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeFalse();
    }

    [Fact]
    public void HasImpactingChanges_WhenExporterChanges()
    {
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: null,
            updatedExporter: PreviousExporter, // We don't check for "real" differences, assume all changes matter
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesEnv()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.Environment, "new" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesServiceName()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.ServiceName, "service" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesServiceVersion()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.ServiceVersion, "1.0.0" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void HasImpactingChanges_WhenMutableChangesGlobalTags()
    {
        var newSettings = MutableSettings.CreateForTesting(TracerSettings, new() { { ConfigurationKeys.GlobalTags, "a:b" } });
        var changes = new TracerSettings.SettingsManager.SettingChanges(
            updatedMutable: newSettings,
            updatedExporter: null,
            PreviousMutable,
            PreviousExporter);
        StatsdManager.HasImpactingChanges(changes).Should().BeTrue();
    }

    [Fact]
    public void InitialState_ClientNotCreated()
    {
        var clientCount = 0;
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        var lease = manager.TryGetClientLease();

        lease.Client.Should().BeNull("client should not be created unless required");
        clientCount.Should().Be(0, "factory should not be called");
    }

    [Fact]
    public void SetRequired_CreatesClient()
    {
        var clientCount = 0;
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        var lease = manager.TryGetClientLease();

        lease.Client.Should().NotBeNull("client should be created when required");
        clientCount.Should().Be(1, "factory should be called exactly once");
    }

    [Fact]
    public void SetRequired_False_DisposesClient()
    {
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => client);

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        using (manager.TryGetClientLease())
        {
        }

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);

        client.IsDisposed.Should().BeTrue("client should be disposed when no longer required and not in use");
    }

    [Fact]
    public void MultipleConsumers_AllRequire_SingleClient()
    {
        var clientCount = 0;
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        manager.SetRequired(StatsdConsumer.TraceApi, true);
        manager.SetRequired(StatsdConsumer.AgentWriter, true);

        clientCount.Should().Be(1, "only one client should be created for multiple consumers");
    }

    [Fact]
    public void MultipleConsumers_PartialUnrequire_KeepsClient()
    {
        var clientCount = 0;
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        manager.SetRequired(StatsdConsumer.TraceApi, true);

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);

        clientCount.Should().Be(1, "only one client should be created for multiple consumers");
        client.IsDisposed.Should().BeFalse("client should remain when at least one consumer requires it");
    }

    [Fact]
    public void MultipleConsumers_AllUnrequire_DisposesClient()
    {
        var clientCount = 0;
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return client;
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        manager.SetRequired(StatsdConsumer.TraceApi, true);

        using (manager.TryGetClientLease())
        {
            manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
            manager.SetRequired(StatsdConsumer.TraceApi, false);

            client.IsDisposed.Should().BeFalse("client should be disposed while it is leased");
        }

        client.IsDisposed.Should().BeTrue("client should be disposed when all consumers unrequire it");
    }

    [Fact]
    public void MultipleConsumers_ReRequire_CreatesNewClient()
    {
        var clientCount = 0;
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        clientCount.Should().Be(3);
        client.IsDisposed.Should().BeFalse("client should remain when at least one consumer requires it");
    }

    [Fact]
    public void Lease_ProvidesAccessToClient()
    {
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => client);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        using var lease = manager.TryGetClientLease();

        lease.Client.Should().BeSameAs(client);
    }

    [Fact]
    public void MultipleLeasesSimultaneously_ShareSameClient()
    {
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => client);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        using var lease1 = manager.TryGetClientLease();
        using var lease2 = manager.TryGetClientLease();
        using var lease3 = manager.TryGetClientLease();

        lease1.Client.Should().BeSameAs(client);
        lease2.Client.Should().BeSameAs(client);
        lease3.Client.Should().BeSameAs(client);
    }

    [Fact]
    public void DisposingLease_DoesNotDisposeClient_WhileOtherLeasesActive()
    {
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => client);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        var lease1 = manager.TryGetClientLease();
        var lease2 = manager.TryGetClientLease();

        lease1.Dispose();
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);

        client.IsDisposed.Should().BeFalse("client should not be disposed while other leases are active");
        lease2.Dispose();
        client.IsDisposed.Should().BeTrue();
    }

    [Fact]
    public void NeverReturnsDisposedClient()
    {
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => new MockStatsdClient());

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        var lease1 = manager.TryGetClientLease();
        lease1.Dispose();

        // Trigger client recreation
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        // Try to get a new lease
        var lease2 = manager.TryGetClientLease();

        lease2.Client.Should().NotBeNull();
        ((MockStatsdClient)lease2.Client).IsDisposed.Should().BeFalse("should never return a disposed client");

        // Cleanup
        lease2.Dispose();
    }

    [Fact]
    public void ReferenceCountingPreventsDisposalWhileLeasesActive()
    {
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => client);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        var lease = manager.TryGetClientLease();
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);

        client.IsDisposed.Should().BeFalse("client should not be disposed while lease is active");

        lease.Dispose();

        client.IsDisposed.Should().BeTrue("client should be disposed after lease is released");
    }

    [Fact]
    public void Dispose_WithActiveLease_DisposesAfterLeaseReleased()
    {
        var client = new MockStatsdClient();
        var manager = new StatsdManager(new TracerSettings(), (_, _) => client);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        var lease = manager.TryGetClientLease();
        manager.Dispose(); // note _manager_ disposed

        client.IsDisposed.Should().BeFalse("client should not be disposed while lease is active");

        // Dispose the lease
        lease.Dispose();

        // Now it should be disposed
        client.IsDisposed.Should().BeTrue("client should be disposed after lease is released");
    }

    [Fact]
    public void SettingsUpdate_RecreatesClient_WhenRequired()
    {
        var clientCount = 0;
        var tracerSettings = new TracerSettings();
        using var manager = new StatsdManager(tracerSettings, (_, _) =>
        {
            Interlocked.Increment(ref  clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        var lease1 = manager.TryGetClientLease();
        var client1 = lease1.Client;
        lease1.Dispose();

        tracerSettings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new Dictionary<string, object> { { TracerSettingKeyConstants.EnvironmentKey, "test" } },
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        var lease2 = manager.TryGetClientLease();

        clientCount.Should().Be(2, "new client should be created after settings change");
        lease2.Client.Should().NotBeSameAs(client1, "should get a new client after settings update");

        lease2.Dispose();
    }

    [Fact]
    public void SettingsUpdate_OldLeaseContinuesWorkingWithOldClient()
    {
        var tracerSettings = new TracerSettings();
        using var manager = new StatsdManager(tracerSettings, (_, _) => new MockStatsdClient());

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        var lease1 = manager.TryGetClientLease();
        var client1 = lease1.Client;

        tracerSettings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new Dictionary<string, object> { { TracerSettingKeyConstants.EnvironmentKey, "test" } },
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        lease1.Client.Should().BeSameAs(client1, "old lease should continue to reference old client");
        ((MockStatsdClient)client1).IsDisposed.Should().BeFalse("old client should not be disposed while lease is active");

        // Get new lease
        var lease2 = manager.TryGetClientLease();
        var client2 = lease2.Client;
        lease2.Client.Should().NotBeSameAs(client1, "new lease should get new client");

        // Cleanup
        lease1.Dispose();
        ((MockStatsdClient)client1).IsDisposed.Should().BeTrue("old client should be disposed after lease is released");
        lease2.Dispose();
        ((MockStatsdClient)client2).IsDisposed.Should().BeFalse("new client is still in use");
    }

    [Fact]
    public void SettingsUpdate_DoesNotRecreateClient_WhenNotRequired()
    {
        var tracerSettings = new TracerSettings();
        var clientCount = 0;
        using var manager = new StatsdManager(tracerSettings, (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        // Don't call SetRequired - no client should be created

        tracerSettings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new Dictionary<string, object> { { TracerSettingKeyConstants.EnvironmentKey, "test" } },
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        clientCount.Should().Be(0, "client should not be created for settings update when not required");
    }

    [Fact]
    public void SettingsUpdate_DoesNotRecreateClient_WhenSettingsDontChange()
    {
        var tracerSettings = new TracerSettings();
        var clientCount = 0;
        using var manager = new StatsdManager(tracerSettings, (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        clientCount.Should().Be(1);

        // Same default settings source

        tracerSettings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(new Dictionary<string, object>(), useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        clientCount.Should().Be(1, "client should not be recreated for settings update when no changes");
    }

    [Fact]
    public void SettingsUpdate_DoesNotRecreateClient_WhenRelevantSettingsDontChange()
    {
        var tracerSettings = new TracerSettings();
        var clientCount = 0;
        using var manager = new StatsdManager(tracerSettings, (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        clientCount.Should().Be(1);

        // Header tags does not affect statsdclient
        tracerSettings.Manager.UpdateManualConfigurationSettings(
            new ManualInstrumentationConfigurationSource(
                new Dictionary<string, object> { { TracerSettingKeyConstants.HeaderTags, "some-header" } },
                useDefaultSources: true),
            NullConfigurationTelemetry.Instance);

        clientCount.Should().Be(1, "client should not be recreated for settings update when no changes");
    }

    [Fact]
    public void ConcurrentLeaseAcquisition_AllSucceed()
    {
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => client);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        var leases = new ConcurrentQueue<StatsdManager.StatsdClientLease>();
        Parallel.For(0, 100, _ =>
        {
            var lease = manager.TryGetClientLease();
            leases.Enqueue(lease);
        });

        leases.Should().HaveCount(100);
        leases.Should().AllSatisfy(lease => lease.Client.Should().BeSameAs(client));

        // Cleanup
        while (leases.TryDequeue(out var lease))
        {
            lease.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentLeaseAcquisitionAndDisposal_ThreadSafe()
    {
        var clientCount = 0;
        var client = new MockStatsdClient();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return client;
        });
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        var cts = new CancellationTokenSource();

        var tasks = new[]
        {
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var lease = manager.TryGetClientLease();
                    lease.Dispose();
                }
            }),
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var lease = manager.TryGetClientLease();
                    lease.Dispose();
                }
            }),
            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    var lease = manager.TryGetClientLease();
                    lease.Dispose();
                }
            })
        };

        Thread.Sleep(100);
        cts.Cancel();

        await Task.WhenAll(tasks);
        clientCount.Should().Be(1, "client should not be recreated for settings update when no changes");
        client.IsDisposed.Should().BeFalse("client should not be disposed while still required");
    }

    [Fact]
    public void ConcurrentSetRequired_ThreadSafe()
    {
        var clientCount = 0;
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        // random toggling on an off
        Parallel.For(0, 50, i =>
        {
            manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, i % 2 == 0);
        });

        Parallel.For(0, 50, i =>
        {
            manager.SetRequired(StatsdConsumer.TraceApi, i % 3 == 0);
        });

        // The exact client count is non-deterministic, but should be reasonable
        clientCount.Should().BeLessThan(50, "should not create excessive clients");
    }

    [Fact]
    public async Task ConcurrentSettingsUpdateAndLeaseAcquisition_ThreadSafe()
    {
        var tracerSettings = new TracerSettings();
        var clientCount = 0;
        using var manager = new StatsdManager(tracerSettings, (_, _) =>
        {
            Interlocked.Increment(ref clientCount);
            return new MockStatsdClient();
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
        var cts = new CancellationTokenSource();
        var disposedClientReturned = 0;

        var tasks = new[]
        {
            Task.Run(() =>
            {
                // Update the environment continuously
                var counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    tracerSettings.Manager.UpdateManualConfigurationSettings(
                        new ManualInstrumentationConfigurationSource(
                            new Dictionary<string, object> { { TracerSettingKeyConstants.EnvironmentKey, $"env{counter++}" } },
                            useDefaultSources: true),
                        NullConfigurationTelemetry.Instance);
                }
            }),

            Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    using var lease = manager.TryGetClientLease();
                    if (lease.Client != null)
                    {
                        // Check if client is disposed WHILE we hold the lease
                        if (((MockStatsdClient)lease.Client).IsDisposed)
                        {
                            Interlocked.Increment(ref disposedClientReturned);
                        }
                    }
                }
            })
        };

        await Task.Delay(500);
        cts.Cancel();

        await Task.WhenAll(tasks);
        disposedClientReturned.Should().Be(0, "should never return a disposed client while holding a lease");
    }

    [Fact]
    public async Task ConcurrentLeaseDisposalDuringClientRecreation_ThreadSafe()
    {
        var clients = new ConcurrentQueue<MockStatsdClient>();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            var client = new MockStatsdClient();
            clients.Enqueue(client);
            return client;
        });

        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        // Create a bunch of leases
        var leases = Enumerable.Range(0, 10)
            .Select(_ => manager.TryGetClientLease())
            .ToList();

        var random = new Random();

        var mutex = new CountdownEvent(leases.Count + 1);
        var tasks = leases.Select(lease => Task.Run(() =>
        {
            mutex.Signal(); // decrement
            mutex.Wait(); // wait for count to hit zero
            Thread.Sleep(random.Next(1, 10));
            lease.Dispose();
        })).ToList();

        // Wait and then do everything at once
        mutex.Signal();
        mutex.Wait();

        // Trigger recreation while leases are being disposed
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        await Task.WhenAll(tasks);

        // We only recreated once
        clients.Should().HaveCount(2);
        clients.TryDequeue(out var client1).Should().BeTrue();
        client1.IsDisposed.Should().BeTrue("old client should be disposed");
        clients.TryDequeue(out var client2).Should().BeTrue();
        client2.IsDisposed.Should().BeFalse("latest client should not be disposed");
    }

    [Fact]
    public void MultipleTransitionsBetweenRequiredAndNotRequired()
    {
        var clients = new List<MockStatsdClient>();
        using var manager = new StatsdManager(new TracerSettings(), (_, _) =>
        {
            var client = new MockStatsdClient();
            clients.Add(client);
            return client;
        });

        for (var i = 0; i < 5; i++)
        {
            manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);
            var lease = manager.TryGetClientLease();
            lease.Dispose();
            manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
        }

        clients.Count.Should().Be(5, "should create a new client for each transition");
        clients.Should().AllSatisfy(client => client.IsDisposed.Should().BeTrue("all old clients should be disposed"));
    }

    [Fact]
    public void Dispose_MultipleTimes_IsSafe()
    {
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => new MockStatsdClient());
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        manager.Dispose();
        manager.Dispose();
        manager.Dispose();
    }

    [Fact]
    public void DefaultLease_CanDisposeSafely()
    {
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => new MockStatsdClient());

        var lease = manager.TryGetClientLease();

        lease.Client.Should().BeNull();
        lease.Dispose();
    }

    [Fact]
    public void DisposingLease_MultipleTimes_DoesNotDisposeStatsDMultipleTimes()
    {
        using var manager = new StatsdManager(new TracerSettings(), (_, _) => new MockStatsdClient());
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, true);

        var lease = manager.TryGetClientLease();
        var client = lease.Client.Should().NotBeNull().And.BeOfType<MockStatsdClient>().Subject;
        manager.SetRequired(StatsdConsumer.RuntimeMetricsWriter, false);
        lease.Dispose();
        lease.Dispose();
        lease.Dispose();
        client.DisposeCount.Should().Be(1);
    }

    private class MockStatsdClient : IDogStatsd
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public bool IsDisposed => DisposeCount > 0;

        public ITelemetryCounters TelemetryCounters => null;

        public void Configure(StatsdConfig config)
        {
        }

        public void Counter(string statName, double value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Decrement(string statName, int value = 1, double sampleRate = 1, params string[] tags)
        {
        }

        public void Event(string title, string text, string alertType = null, string aggregationKey = null, string sourceType = null, int? dateHappened = null, string priority = null, string hostname = null, string[] tags = null)
        {
        }

        public void Gauge(string statName, double value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Histogram(string statName, double value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Distribution(string statName, double value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Increment(string statName, int value = 1, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Set<T>(string statName, T value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void Set(string statName, string value, double sampleRate = 1, string[] tags = null)
        {
        }

        public IDisposable StartTimer(string name, double sampleRate = 1, string[] tags = null)
        {
            return null;
        }

        public void Time(Action action, string statName, double sampleRate = 1, string[] tags = null)
        {
        }

        public T Time<T>(Func<T> func, string statName, double sampleRate = 1, string[] tags = null)
        {
            return func();
        }

        public void Timer(string statName, double value, double sampleRate = 1, string[] tags = null)
        {
        }

        public void ServiceCheck(string name, Status status, int? timestamp = null, string hostname = null, string[] tags = null, string message = null)
        {
        }

        public void Dispose()
        {
            Interlocked.Increment(ref _disposeCount);
        }
    }
}
