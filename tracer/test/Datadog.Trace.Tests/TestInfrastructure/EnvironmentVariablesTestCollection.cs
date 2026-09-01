// <copyright file="EnvironmentVariablesTestCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Xunit;

namespace Datadog.Trace.Tests;

/// <summary>
/// Used to indicate a test modifies environment variables, so shouldn't be run in parallel with other similar tests
/// </summary>
[CollectionDefinition(nameof(EnvironmentVariablesTestCollection), DisableParallelization = true)]
public class EnvironmentVariablesTestCollection
{
}
