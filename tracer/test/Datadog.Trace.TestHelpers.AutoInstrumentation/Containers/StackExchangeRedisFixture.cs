// <copyright file="StackExchangeRedisFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class StackExchangeRedisFixture : ContainerFixture
{
    private const int RedisPort = 6379;
    private const string PrimaryAlias = "stackexchangeredis";
    private const string DefaultHostConfiguration = "localhost:6379,localhost:6380";

    private Endpoint? _primaryEndpoint;
    private Endpoint? _replicaEndpoint;
    private string? _hostConfiguration;
    private string? _singleHostConfiguration;

    public string PrimaryHost => _primaryEndpoint!.Host;

    public ushort PrimaryPort => _primaryEndpoint!.Port;

    public string ReplicaHost => _replicaEndpoint!.Host;

    public ushort ReplicaPort => _replicaEndpoint!.Port;

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        if (_hostConfiguration is null || _singleHostConfiguration is null)
        {
            yield break;
        }

        yield return new("STACKEXCHANGE_REDIS_HOST", _hostConfiguration);
        yield return new("STACKEXCHANGE_REDIS_SINGLE_HOST", _singleHostConfiguration);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Windows tests use an existing Redis instance because CI does not provide Docker.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var hostConfiguration = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_HOST");
            _hostConfiguration = string.IsNullOrEmpty(hostConfiguration) ? DefaultHostConfiguration : hostConfiguration;
            var endpoints = ParseEndpoints(_hostConfiguration);
            _primaryEndpoint = endpoints[0];
            _replicaEndpoint = endpoints.Count > 1 ? endpoints[1] : endpoints[0];
            var singleHostConfiguration = Environment.GetEnvironmentVariable("STACKEXCHANGE_REDIS_SINGLE_HOST");
            _singleHostConfiguration = string.IsNullOrEmpty(singleHostConfiguration) ? endpoints[0].ToString() : singleHostConfiguration;
            return;
        }

        // Keep synchronized with the image version in docker-compose.yml.
        const string image = "redis:4-alpine";

        var network = new NetworkBuilder().Build();
        var primaryContainer = new ContainerBuilder(image)
                              .WithCommand("redis-server", "--bind", "0.0.0.0")
                              .WithNetwork(network)
                              .WithNetworkAliases(PrimaryAlias)
                              .WithPortBinding(RedisPort, true)
                              .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
                              .Build();
        var replicaContainer = new ContainerBuilder(image)
                              .WithCommand("redis-server", "--bind", "0.0.0.0", "--slaveof", PrimaryAlias, "6379")
                              .WithNetwork(network)
                              .WithPortBinding(RedisPort, true)
                              .WithWaitStrategy(
                                   Wait.ForUnixContainer()
                                       .UntilInternalTcpPortIsAvailable(RedisPort)
                                       .UntilCommandIsCompleted("sh", "-c", "redis-cli info replication | grep -q 'master_link_status:up'"))
                              .Build();
        var singleContainer = new ContainerBuilder(image)
                             .WithCommand("redis-server", "--bind", "0.0.0.0")
                             .WithNetwork(network)
                             .WithPortBinding(RedisPort, true)
                             .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(RedisPort))
                             .Build();
        var resources = new Resources(primaryContainer, replicaContainer, singleContainer, network);

        try
        {
            await network.CreateAsync().ConfigureAwait(false);
            await primaryContainer.StartAsync().ConfigureAwait(false);
            await Task.WhenAll(replicaContainer.StartAsync(), singleContainer.StartAsync()).ConfigureAwait(false);
        }
        catch
        {
            await resources.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        _primaryEndpoint = new Endpoint(primaryContainer.Hostname, primaryContainer.GetMappedPublicPort(RedisPort));
        _replicaEndpoint = new Endpoint(replicaContainer.Hostname, replicaContainer.GetMappedPublicPort(RedisPort));
        _hostConfiguration = $"{_primaryEndpoint},{_replicaEndpoint}";
        _singleHostConfiguration = $"{singleContainer.Hostname}:{singleContainer.GetMappedPublicPort(RedisPort)}";
        registerResource("resources", resources);
    }

    private static List<Endpoint> ParseEndpoints(string configuration)
    {
        var endpoints = new List<Endpoint>();
        foreach (var value in configuration.Split(','))
        {
            var trimmedValue = value.Trim();
            if (trimmedValue.Length == 0 || trimmedValue.Contains("="))
            {
                continue;
            }

            endpoints.Add(Endpoint.Parse(trimmedValue));
            if (endpoints.Count == 2)
            {
                break;
            }
        }

        if (endpoints.Count == 0)
        {
            throw new InvalidOperationException("STACKEXCHANGE_REDIS_HOST must contain at least one Redis endpoint.");
        }

        return endpoints;
    }

    private sealed class Endpoint
    {
        public Endpoint(string host, ushort port)
        {
            Host = host;
            Port = port;
        }

        public string Host { get; }

        public ushort Port { get; }

        public static Endpoint Parse(string value)
        {
            if (value[0] == '[')
            {
                var closingBracketIndex = value.IndexOf(']');
                if (closingBracketIndex > 0)
                {
                    var host = value.Substring(1, closingBracketIndex - 1);
                    var portValue = value.Substring(closingBracketIndex + 1).TrimStart(':');
                    return new Endpoint(host, ParsePort(portValue));
                }
            }

            var separatorIndex = value.LastIndexOf(':');
            if (separatorIndex > 0 && ushort.TryParse(value.Substring(separatorIndex + 1), out var port))
            {
                return new Endpoint(value.Substring(0, separatorIndex), port);
            }

            return new Endpoint(value, RedisPort);
        }

        public override string ToString() => Host.IndexOf(':') >= 0 ? $"[{Host}]:{Port}" : $"{Host}:{Port}";

        private static ushort ParsePort(string value) => ushort.TryParse(value, out var port) ? port : (ushort)RedisPort;
    }

    private sealed class Resources : IAsyncDisposable
    {
        public Resources(IContainer primaryContainer, IContainer replicaContainer, IContainer singleContainer, INetwork network)
        {
            PrimaryContainer = primaryContainer;
            ReplicaContainer = replicaContainer;
            SingleContainer = singleContainer;
            Network = network;
        }

        public IContainer PrimaryContainer { get; }

        public IContainer ReplicaContainer { get; }

        public IContainer SingleContainer { get; }

        private INetwork Network { get; }

        public async ValueTask DisposeAsync()
        {
            await SingleContainer.DisposeAsync().ConfigureAwait(false);
            await ReplicaContainer.DisposeAsync().ConfigureAwait(false);
            await PrimaryContainer.DisposeAsync().ConfigureAwait(false);
            await Network.DisposeAsync().ConfigureAwait(false);
        }
    }
}
