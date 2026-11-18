// <copyright file="Elasticsearch7Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class Elasticsearch7Fixture : ElasticsearchFixture
{
    private const string X64Image = "docker.elastic.co/elasticsearch/elasticsearch:7.14.1@sha256:2dcd2f31e246a8b13995ba24922da2edc3d88e65532ff301d0b92cb1be358af5";

    public Elasticsearch7Fixture()
        : base("ELASTICSEARCH7_HOST", SelectImage(X64Image))
    {
    }
}
