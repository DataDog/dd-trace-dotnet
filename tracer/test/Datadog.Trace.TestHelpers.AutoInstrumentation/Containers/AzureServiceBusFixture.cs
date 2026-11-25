// <copyright file="AzureServiceBusFixture.cs" company="Datadog">
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

public class AzureServiceBusFixture : ContainerFixture
{
    private const int ServiceBusAmqpPort = 5672;
    private const int ServiceBusHealthPort = 5300;
    private const int SqlServerPort = 1433;
    private const string Password = "Strong!Passw0rd";
    private const string SqlEdgeAlias = "sqledge";
    private const string SqlEdgeImage = "mcr.microsoft.com/azure-sql-edge:latest@sha256:902628a8be89e35dfb7895ca31d602974c7bafde4d583a0d0873844feb1c42cf";
    private const string ServiceBusImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:1.1.2@sha256:353913ece3d9124cebd40f4b91d00dd197846b8cf86eae9a4790698709c64a1d";
    private const string ServiceBusConfigContainerPath = "/ServiceBus_Emulator/ConfigFiles/Config.json";

    public string ServiceBusConnectionString
        => $"Endpoint=sb://{ServiceBusHostname}:{ServiceBusContainer.GetMappedPublicPort(ServiceBusAmqpPort)};SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public string ServiceBusHostname => ServiceBusContainer.Hostname;

    private IContainer ServiceBusContainer => GetResource<IContainer>("servicebus");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("ASB_CONNECTION_STRING", ServiceBusConnectionString);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep image versions synchronized with docker-compose.yml.
        var network = new NetworkBuilder().Build();
        var sqlEdgeContainer = new ContainerBuilder(SqlEdgeImage)
                              .WithNetwork(network)
                              .WithNetworkAliases(SqlEdgeAlias)
                              .WithEnvironment("ACCEPT_EULA", "Y")
                              .WithEnvironment("MSSQL_SA_PASSWORD", Password)
                              .WithWaitStrategy(
                                   Wait.ForUnixContainer()
                                       .UntilMessageIsLogged(
                                            "SQL Server is now ready for client connections",
                                            strategy => strategy.WithTimeout(TimeSpan.FromMinutes(2))))
                              .Build();

        var configPath = Path.Combine(TestHelpers.EnvironmentTools.GetSolutionDirectory(), "docker", "servicebus-emulator-config.json");
        var configBytes = File.ReadAllBytes(configPath);
        var serviceBusContainer = new ContainerBuilder(ServiceBusImage)
                                 .WithNetwork(network)
                                 .WithEnvironment("ACCEPT_EULA", "Y")
                                 .WithEnvironment("SQL_SERVER", $"{SqlEdgeAlias}:{SqlServerPort}")
                                 .WithEnvironment("MSSQL_SA_PASSWORD", Password)
                                 .WithResourceMapping(configBytes, ServiceBusConfigContainerPath)
                                 .WithPortBinding(ServiceBusAmqpPort, true)
                                 .WithPortBinding(ServiceBusHealthPort, true)
                                 .WithWaitStrategy(
                                      Wait.ForUnixContainer()
                                          .AddCustomWaitStrategy(new ServiceBusHealthWaitStrategy()))
                                 .Build();

        registerResource("network", network);
        registerResource("sqledge", sqlEdgeContainer);
        registerResource("servicebus", serviceBusContainer);

        await network.CreateAsync().ConfigureAwait(false);
        await StartContainerAsync(sqlEdgeContainer).ConfigureAwait(false);
        await StartContainerAsync(serviceBusContainer).ConfigureAwait(false);
    }

    private sealed class ServiceBusHealthWaitStrategy : IWaitUntil
    {
        public async Task<bool> UntilAsync(IContainer container)
        {
            using var handler = new HttpClientHandler { UseProxy = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2) };
            var healthEndpoint = new UriBuilder(
                Uri.UriSchemeHttp,
                container.Hostname,
                container.GetMappedPublicPort(ServiceBusHealthPort),
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
