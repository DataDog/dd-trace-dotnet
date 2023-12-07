// <copyright file="CouchbaseFixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.Containers
{
    public class CouchbaseFixture : ContainerFixture
    {
        protected IContainer Container => GetResource<IContainer>("container");

        public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
        {
            yield return new("COUCHBASE_HOST", Container.Hostname);
            yield return new("COUCHBASE_PORT", Container.GetMappedPublicPort(8091).ToString());
        }

        protected override async Task InitializeResources(Action<string, object> registerResource)
        {
            var waitStrategy = Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request
                => request
                    .ForPath("/pools/default")
                    .ForPort(8091)
                    .ForResponseMessageMatching(IsNodeHealthyAsync)
                    .WithBasicAuthentication("default", "password"));

            var container = new ContainerBuilder()
                .WithImage("bentonam/couchbase-docker:community-5.0.1")
                .WithName("couchbase")
                .WithPortBinding(8091)
                .WithPortBinding(8092)
                .WithPortBinding(8093)
                .WithPortBinding(8094)
                .WithPortBinding(11210)
                .WithWaitStrategy(waitStrategy)
                .Build();

            await container.StartAsync();

            registerResource("container", container);
        }

        private static async Task<bool> IsNodeHealthyAsync(HttpResponseMessage response)
        {
            var jsonString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            try
            {
                var status = JsonDocument.Parse(jsonString)
                    .RootElement
                    .GetProperty("nodes")
                    .EnumerateArray()
                    .ElementAt(0)
                    .GetProperty("status")
                    .GetString();

                return "healthy".Equals(status);
            }
            catch
            {
                return false;
            }
        }
    }
}
