// <copyright file="Elasticsearch7Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class Elasticsearch7Fixture : ElasticsearchFixtureBase
{
    protected override string ImageTag => "7.17.18";

    protected override string EnvironmentVariableName => "ELASTICSEARCH7_HOST";
}
