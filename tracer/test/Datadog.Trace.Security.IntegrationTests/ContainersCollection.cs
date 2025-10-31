// <copyright file="ContainersCollection.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
using Xunit;

namespace Datadog.Trace.Security.IntegrationTests;

/// <summary>
/// Collection definition for TestContainers.
/// This ensures that all containers are properly disposed when all tests complete.
/// </summary>
[CollectionDefinition(Name)]
public class ContainersCollection : ICollectionFixture<ContainersCleanup>
{
    public const string Name = "TestContainers";
}

/// <summary>
/// Cleanup fixture that disposes all TestContainers when the test collection completes.
/// This implements the optimization mentioned in PR #5031: "stop the docker images as soon as they're not needed anymore"
/// </summary>
#pragma warning disable SA1402 // File may only contain a single type
public class ContainersCleanup : IAsyncLifetime
#pragma warning restore SA1402 // File may only contain a single type
{
    public Task InitializeAsync()
    {
        // No initialization needed
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        try
        {
            await ContainersRegistry.DisposeAll();
        }
        catch
        {
            // Don't throw - we don't want to fail tests due to cleanup errors
        }
    }
}
