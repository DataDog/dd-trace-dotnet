// <copyright file="Elasticsearch5Fixture.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

/// <summary>
/// Provides Elasticsearch containers for integration tests.
/// Keep synchronized image versions with docker-compose.yml
/// </summary>
public class Elasticsearch5Fixture : ElasticsearchFixtureBase
{
    protected override string ImageTag => "5.6.16";

    protected override string EnvironmentVariableName => "ELASTICSEARCH5_HOST";
}
