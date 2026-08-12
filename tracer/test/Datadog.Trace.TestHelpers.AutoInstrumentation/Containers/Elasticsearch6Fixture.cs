// <copyright file="Elasticsearch6Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class Elasticsearch6Fixture : ElasticsearchFixture
{
    private const string Image = "docker.elastic.co/elasticsearch/elasticsearch:6.4.2@sha256:3da16b2f3b1d4e151c44f1a54f4f29d8be64884a64504b24ebcbdb4e14c80aa1";

    public Elasticsearch6Fixture()
        : base("ELASTICSEARCH6_HOST", SelectImage(Image))
    {
    }
}
