// <copyright file="CouchbaseFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class CouchbaseFixture : ContainerFixture
{
    private const ushort ManagementPort = 8091;
    private const ushort ManagementSslPort = 18091;
    private const ushort ViewPort = 8092;
    private const ushort ViewSslPort = 18092;
    private const ushort QueryPort = 8093;
    private const ushort QuerySslPort = 18093;
    private const ushort KeyValuePort = 11210;
    private const ushort KeyValueSslPort = 11207;
    private const string Image = "couchbase:community-6.6.0@sha256:43103efdd4b562366c7a48afa977c4ad148e09c877da28a83a86b2c5ee2daa97";
    private const string AdministratorUsername = "Administrator";
    private const string Username = "default";
    private const string Password = "password";

    private string? _host;
    private ushort _managementPort;
    private ushort _keyValuePort;

    public string Host => _host!;

    public string BucketName => "default";

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("COUCHBASE_HOST", Host);
        yield return new("COUCHBASE_PORT", _managementPort.ToString());
        yield return new("COUCHBASE_CONNECTION_STRING", $"{Host}:{_keyValuePort}");
        yield return new("COUCHBASE_USERNAME", Username);
        yield return new("COUCHBASE_PASSWORD", Password);
        yield return new("COUCHBASE_BUCKET", BucketName);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // When these tests run in the outer Compose container, Testcontainers reaches published ports through the
        // Docker host. Couchbase also binds its configured node hostname, so it must share the host network in that
        // scenario. Otherwise, trying to configure the Docker host address as the node hostname fails with eaddrnotavail.
        var useHostNetwork = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                          && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CONTAINER_HOSTNAME"));
        var dockerHost = useHostNetwork ? GetDockerHost() : null;
        var builder = new ContainerBuilder(Image)
                     .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ManagementPort));

        if (useHostNetwork)
        {
            builder = builder.WithCreateParameterModifier(p =>
            {
                p.HostConfig ??= new HostConfig();
                p.HostConfig.NetworkMode = "host";
            });
        }
        else
        {
            // SDK 2.4 predates Couchbase's external-network selection, so it requires the advertised standard ports.
            builder = builder.WithPortBinding(ManagementPort, false)
                             .WithPortBinding(ManagementSslPort, false)
                             .WithPortBinding(ViewPort, false)
                             .WithPortBinding(ViewSslPort, false)
                             .WithPortBinding(QueryPort, false)
                             .WithPortBinding(QuerySslPort, false)
                             .WithPortBinding(KeyValuePort, false)
                             .WithPortBinding(KeyValueSslPort, false);
        }

        var container = builder.Build();

        registerResource("container", container);
        await container.StartAsync().ConfigureAwait(false);
        var host = dockerHost ?? container.Hostname;
        var managementPort = useHostNetwork ? ManagementPort : container.GetMappedPublicPort(ManagementPort);
        var keyValuePort = useHostNetwork ? KeyValuePort : container.GetMappedPublicPort(KeyValuePort);

        await ConfigureCouchbaseAsync(container, host, useHostNetwork).ConfigureAwait(false);

        _host = host;
        _managementPort = managementPort;
        _keyValuePort = keyValuePort;
    }

    private static string GetDockerHost()
        => NetworkInterface.GetAllNetworkInterfaces()
                           .SelectMany(networkInterface => networkInterface.GetIPProperties().GatewayAddresses)
                           .Select(gateway => gateway.Address)
                           .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork
                                                   && !IPAddress.IsLoopback(address))?.ToString()
        ?? throw new InvalidOperationException("Unable to determine the Docker host from the container's default gateway.");

    private static async Task ConfigureCouchbaseAsync(IContainer container, string host, bool useHostNetwork)
    {
        var managementPort = useHostNetwork ? ManagementPort : container.GetMappedPublicPort(ManagementPort);
        var managementSslPort = useHostNetwork ? ManagementSslPort : container.GetMappedPublicPort(ManagementSslPort);
        var viewPort = useHostNetwork ? ViewPort : container.GetMappedPublicPort(ViewPort);
        var viewSslPort = useHostNetwork ? ViewSslPort : container.GetMappedPublicPort(ViewSslPort);
        var queryPort = useHostNetwork ? QueryPort : container.GetMappedPublicPort(QueryPort);
        var querySslPort = useHostNetwork ? QuerySslPort : container.GetMappedPublicPort(QuerySslPort);
        var keyValuePort = useHostNetwork ? KeyValuePort : container.GetMappedPublicPort(KeyValuePort);
        var keyValueSslPort = useHostNetwork ? KeyValueSslPort : container.GetMappedPublicPort(KeyValueSslPort);

        using var client = new HttpClient
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttp, host, managementPort).Uri,
            Timeout = TimeSpan.FromSeconds(5),
        };

        await WaitForSuccessAsync(client, "/pools").ConfigureAwait(false);
        await PostFormAsync(client, "/node/controller/rename", new() { ["hostname"] = host }).ConfigureAwait(false);
        await PostFormAsync(client, "/node/controller/setupServices", new() { ["services"] = "kv,index,n1ql" }).ConfigureAwait(false);
        await PostFormAsync(client, "/pools/default", new() { ["memoryQuota"] = "256", ["indexMemoryQuota"] = "256" }).ConfigureAwait(false);
        await PutFormAsync(
             client,
             "/node/controller/setupAlternateAddresses/external",
             new()
             {
                 ["hostname"] = host,
                 ["mgmt"] = managementPort.ToString(),
                 ["mgmtSSL"] = managementSslPort.ToString(),
                 ["kv"] = keyValuePort.ToString(),
                 ["kvSSL"] = keyValueSslPort.ToString(),
                 ["capi"] = viewPort.ToString(),
                 ["capiSSL"] = viewSslPort.ToString(),
                 ["n1ql"] = queryPort.ToString(),
                 ["n1qlSSL"] = querySslPort.ToString(),
             }).ConfigureAwait(false);
        await PostFormAsync(
             client,
             "/pools/default/buckets",
             new() { ["name"] = "default", ["ramQuotaMB"] = "100", ["replicaNumber"] = "0" }).ConfigureAwait(false);
        await PostFormAsync(
             client,
             "/settings/web",
             new() { ["username"] = AdministratorUsername, ["password"] = Password, ["port"] = "SAME" }).ConfigureAwait(false);

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AdministratorUsername}:{Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        await PutFormAsync(
             client,
             $"/settings/rbac/users/local/{Username}",
             new() { ["name"] = Username, ["password"] = Password, ["roles"] = "admin" }).ConfigureAwait(false);
        await WaitForSuccessAsync(
             client,
             "/pools/default/buckets/default",
             content => content.IndexOf(@"""status"":""healthy""", StringComparison.OrdinalIgnoreCase) >= 0).ConfigureAwait(false);

        using var queryClient = new HttpClient
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttp, host, queryPort).Uri,
            Timeout = TimeSpan.FromSeconds(5),
        };
        queryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        await WaitForSuccessAsync(queryClient, "/admin/ping").ConfigureAwait(false);
        await WaitForSuccessAsync(
             client,
             "/pools/default/nodeServices",
             content => content.IndexOf(@"""n1ql"":8093", StringComparison.OrdinalIgnoreCase) >= 0).ConfigureAwait(false);
    }

    private static Task PostFormAsync(HttpClient client, string path, Dictionary<string, string> form)
        => SendFormAsync(client, HttpMethod.Post, path, form);

    private static Task PutFormAsync(HttpClient client, string path, Dictionary<string, string> form)
        => SendFormAsync(client, HttpMethod.Put, path, form);

    private static async Task SendFormAsync(HttpClient client, HttpMethod method, string path, Dictionary<string, string> form)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = new FormUrlEncodedContent(form.Select(static pair => new KeyValuePair<string?, string?>(pair.Key, pair.Value))),
        };
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            throw new InvalidOperationException($"Couchbase request '{path}' failed with status {(int)response.StatusCode}: {content}");
        }
    }

    private static async Task WaitForSuccessAsync(HttpClient client, string path, Func<string, bool>? responsePredicate = null)
    {
        var timeout = DateTime.UtcNow.AddMinutes(2);

        while (DateTime.UtcNow < timeout)
        {
            try
            {
                using var response = await client.GetAsync(path).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (responsePredicate is null || responsePredicate(content))
                    {
                        return;
                    }
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }

        throw new TimeoutException($"Couchbase endpoint '{path}' did not become ready.");
    }
}
