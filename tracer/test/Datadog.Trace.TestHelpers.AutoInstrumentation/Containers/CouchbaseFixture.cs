// <copyright file="CouchbaseFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class CouchbaseFixture : ContainerFixture
{
    private const int ManagementPort = 8091;
    private const int ManagementSslPort = 18091;
    private const int ViewPort = 8092;
    private const int ViewSslPort = 18092;
    private const int QueryPort = 8093;
    private const int QuerySslPort = 18093;
    private const int KeyValuePort = 11210;
    private const int KeyValueSslPort = 11207;
    private const string Image = "couchbase:community-6.6.0@sha256:43103efdd4b562366c7a48afa977c4ad148e09c877da28a83a86b2c5ee2daa97";
    private const string AdministratorUsername = "Administrator";
    private const string Username = "default";
    private const string Password = "password";

    public string Host => Container.Hostname;

    public string BucketName => "default";

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("COUCHBASE_HOST", Host);
        yield return new("COUCHBASE_PORT", Container.GetMappedPublicPort(ManagementPort).ToString());
        yield return new("COUCHBASE_CONNECTION_STRING", $"{Host}:{Container.GetMappedPublicPort(KeyValuePort)}");
        yield return new("COUCHBASE_USERNAME", Username);
        yield return new("COUCHBASE_PASSWORD", Password);
        yield return new("COUCHBASE_BUCKET", BucketName);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(Image)
                       // SDK 2.4 predates Couchbase's external-network selection, so it requires the advertised standard ports.
                       .WithPortBinding(ManagementPort, false)
                       .WithPortBinding(ManagementSslPort, false)
                       .WithPortBinding(ViewPort, false)
                       .WithPortBinding(ViewSslPort, false)
                       .WithPortBinding(QueryPort, false)
                       .WithPortBinding(QuerySslPort, false)
                       .WithPortBinding(KeyValuePort, false)
                       .WithPortBinding(KeyValueSslPort, false)
                       .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(ManagementPort))
                       .Build();

        try
        {
            await container.StartAsync().ConfigureAwait(false);
            await ConfigureCouchbaseAsync(container).ConfigureAwait(false);
        }
        catch
        {
            await container.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        registerResource("container", container);
    }

    private static async Task ConfigureCouchbaseAsync(IContainer container)
    {
        using var client = new HttpClient
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(ManagementPort)).Uri,
            Timeout = TimeSpan.FromSeconds(5),
        };

        await WaitForSuccessAsync(client, "/pools").ConfigureAwait(false);
        await PostFormAsync(client, "/node/controller/rename", new() { ["hostname"] = container.Hostname }).ConfigureAwait(false);
        await PostFormAsync(client, "/node/controller/setupServices", new() { ["services"] = "kv,index,n1ql" }).ConfigureAwait(false);
        await PostFormAsync(client, "/pools/default", new() { ["memoryQuota"] = "256", ["indexMemoryQuota"] = "256" }).ConfigureAwait(false);
        await PutFormAsync(
             client,
             "/node/controller/setupAlternateAddresses/external",
             new()
             {
                 ["hostname"] = container.Hostname,
                 ["mgmt"] = container.GetMappedPublicPort(ManagementPort).ToString(),
                 ["mgmtSSL"] = container.GetMappedPublicPort(ManagementSslPort).ToString(),
                 ["kv"] = container.GetMappedPublicPort(KeyValuePort).ToString(),
                 ["kvSSL"] = container.GetMappedPublicPort(KeyValueSslPort).ToString(),
                 ["capi"] = container.GetMappedPublicPort(ViewPort).ToString(),
                 ["capiSSL"] = container.GetMappedPublicPort(ViewSslPort).ToString(),
                 ["n1ql"] = container.GetMappedPublicPort(QueryPort).ToString(),
                 ["n1qlSSL"] = container.GetMappedPublicPort(QuerySslPort).ToString(),
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
             content => content.Contains(@"""status"":""healthy""", StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);

        using var queryClient = new HttpClient
        {
            BaseAddress = new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(QueryPort)).Uri,
            Timeout = TimeSpan.FromSeconds(5),
        };
        queryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        await WaitForSuccessAsync(queryClient, "/admin/ping").ConfigureAwait(false);
        await WaitForSuccessAsync(
             client,
             "/pools/default/nodeServices",
             content => content.Contains(@"""n1ql"":8093", StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
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
        using var response = await client.SendAsync(request).ConfigureAwait(false);

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
