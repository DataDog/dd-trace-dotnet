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
/// Using ICollectionFixture ensures that ONE instance of each fixture is shared across all test classes
/// in the collection, and disposed when all tests complete. This prevents race conditions and
/// eliminates the need for reference counting.
/// </summary>
[CollectionDefinition(Name)]
public class ContainersCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "TestContainers";
}
