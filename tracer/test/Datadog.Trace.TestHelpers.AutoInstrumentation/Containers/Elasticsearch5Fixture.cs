// <copyright file="Elasticsearch5Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class Elasticsearch5Fixture : ElasticsearchFixture
{
    private const string Image = "docker.elastic.co/elasticsearch/elasticsearch:5.6.16@sha256:9ffbb6d9d0f383d70b8249117e5758dcf9c628a5ab3a78fd6a520ef1d0f416a2";
    private const string Password = "changeme";
    private const string Username = "elastic";

    public Elasticsearch5Fixture()
        : base("ELASTICSEARCH5_HOST", SelectImage(Image), Username, Password)
    {
    }
}
