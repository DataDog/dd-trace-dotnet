// <copyright file="AzureEventHubsFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class AzureEventHubsFixture : ContainerFixture
{
    private const int AzuriteBlobPort = 10000;
    private const int AzuriteQueuePort = 10001;
    private const int AzuriteTablePort = 10002;
    private const int EventHubsAmqpPort = 5672;
    private const int EventHubsHealthPort = 5300;
    private const string AzuriteAlias = "azurite";
    private const string AzuriteAccountKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";
    private const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:latest@sha256:647c63a91102a9d8e8000aab803436e1fc85fbb285e7ce830a82ee5d6661cf37";
    private const string EventHubsImage = "mcr.microsoft.com/azure-messaging/eventhubs-emulator:latest@sha256:2c8e0d4dd93a5fc078df2721eeb3e211442d555d61293ac3972df931c6d9333a";
    private const string EventHubsConfigContainerPath = "/Eventhubs_Emulator/ConfigFiles/Config.json";

    public string EventHubsConnectionString
        => $"Endpoint=sb://{EventHubsHostname}:{EventHubsContainer.GetMappedPublicPort(EventHubsAmqpPort)};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public string AzuriteConnectionString
        => $"DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey={AzuriteAccountKey};BlobEndpoint=http://{AzuriteContainer.Hostname}:{AzuriteContainer.GetMappedPublicPort(AzuriteBlobPort)}/devstoreaccount1;QueueEndpoint=http://{AzuriteContainer.Hostname}:{AzuriteContainer.GetMappedPublicPort(AzuriteQueuePort)}/devstoreaccount1;TableEndpoint=http://{AzuriteContainer.Hostname}:{AzuriteContainer.GetMappedPublicPort(AzuriteTablePort)}/devstoreaccount1;";

    public string EventHubsHostname => EventHubsContainer.Hostname;

    private IContainer AzuriteContainer => GetResource<IContainer>("azurite");

    private IContainer EventHubsContainer => GetResource<IContainer>("eventhubs");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("EVENTHUBS_CONNECTION_STRING", EventHubsConnectionString);
        yield return new("AzureWebJobsStorage", AzuriteConnectionString);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep image versions synchronized with docker-compose.yml.
        var network = new NetworkBuilder().Build();
        var azuriteContainer = new ContainerBuilder(AzuriteImage)
                              .WithNetwork(network)
                              .WithNetworkAliases(AzuriteAlias)
                              .WithPortBinding(AzuriteBlobPort, true)
                              .WithPortBinding(AzuriteQueuePort, true)
                              .WithPortBinding(AzuriteTablePort, true)
                              .WithWaitStrategy(
                                   Wait.ForUnixContainer()
                                       .UntilInternalTcpPortIsAvailable(AzuriteBlobPort)
                                       .UntilInternalTcpPortIsAvailable(AzuriteQueuePort)
                                       .UntilInternalTcpPortIsAvailable(AzuriteTablePort))
                              .Build();

        var configPath = Path.Combine(TestHelpers.EnvironmentTools.GetSolutionDirectory(), "docker", "eventhubs-emulator-config.json");
        var configBytes = File.ReadAllBytes(configPath);
        var eventHubsContainer = new ContainerBuilder(EventHubsImage)
                                .WithNetwork(network)
                                .WithEnvironment("ACCEPT_EULA", "Y")
                                .WithEnvironment("BLOB_SERVER", AzuriteAlias)
                                .WithEnvironment("METADATA_SERVER", AzuriteAlias)
                                .WithResourceMapping(configBytes, EventHubsConfigContainerPath)
                                .WithPortBinding(EventHubsAmqpPort, true)
                                .WithPortBinding(EventHubsHealthPort, true)
                                .WithWaitStrategy(
                                     Wait.ForUnixContainer()
                                         .AddCustomWaitStrategy(new EventHubsHealthWaitStrategy()))
                                .Build();

        registerResource("network", network);
        registerResource("azurite", azuriteContainer);
        registerResource("eventhubs", eventHubsContainer);

        await network.CreateAsync().ConfigureAwait(false);
        await StartContainerAsync(azuriteContainer).ConfigureAwait(false);
        await StartContainerAsync(eventHubsContainer).ConfigureAwait(false);
    }

    private sealed class EventHubsHealthWaitStrategy : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            var healthEndpoint = new UriBuilder(
                Uri.UriSchemeHttp,
                container.Hostname,
                container.GetMappedPublicPort(EventHubsHealthPort),
                "/health").Uri;

            try
            {
                using var response = await client.GetAsync(healthEndpoint).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException)
            {
                // The emulator accepts connections before the health endpoint is ready to respond.
                return false;
            }
        }
    }
}
