// <copyright file="ElasticsearchFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using Newtonsoft.Json.Linq;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public abstract class ElasticsearchFixture : ContainerFixture
{
    private const int ElasticsearchPort = 9200;
    private const string Arm64Image = "elasticsearch:7.10.1@sha256:7cd88158f6ac75d43b447fdd98c4eb69483fa7bf1be5616a85fe556262dc864a";
    private readonly string _environmentVariable;
    private readonly string _image;
    private readonly string? _readinessPassword;
    private readonly string? _readinessUsername;

    protected ElasticsearchFixture(string environmentVariable, string image)
    {
        _environmentVariable = environmentVariable;
        _image = image;
    }

    protected ElasticsearchFixture(string environmentVariable, string image, string readinessUsername, string readinessPassword)
        : this(environmentVariable, image)
    {
        _readinessUsername = readinessUsername;
        _readinessPassword = readinessPassword;
    }

    public string Host => Container.Hostname;

    public ushort Port => Container.GetMappedPublicPort(ElasticsearchPort);

    public string HostAndPort => $"{Host}:{Port}";

    private IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new(_environmentVariable, HostAndPort);
    }

    protected static string SelectImage(string image) =>
        RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? Arm64Image : image;

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        var container = new ContainerBuilder(_image)
                       .WithPortBinding(ElasticsearchPort, true)
                       .WithEnvironment("discovery.type", "single-node")
                       .WithEnvironment("ES_JAVA_OPTS", "-Xms512m -Xmx512m")
                       .WithWaitStrategy(
                            Wait.ForUnixContainer()
                                .UntilHttpRequestIsSucceeded(
                                     ConfigureReadinessRequest,
                                     strategy => strategy.WithTimeout(TimeSpan.FromMinutes(2))))
                       .Build();

        registerResource("container", container);
        await StartContainerAsync(container).ConfigureAwait(false);
    }

    private static async Task<bool> IsClusterReady(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JObject.Parse(content).Value<string>("status") is "yellow" or "green";
    }

    private HttpWaitStrategy ConfigureReadinessRequest(HttpWaitStrategy request)
    {
        request.ForPort(ElasticsearchPort)
               .ForPath("/_cluster/health")
               .ForResponseMessageMatching(IsClusterReady);

        return _readinessUsername is not null && _readinessPassword is not null
                   ? request.WithBasicAuthentication(_readinessUsername, _readinessPassword)
                   : request;
    }
}
