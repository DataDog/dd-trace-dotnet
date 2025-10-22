// Copyright (c) Datadog
// Licensed under the Apache 2 License.

using System;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb;
using Datadog.Trace.ClrProfiler.CallTarget;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ClrProfiler.AutoInstrumentation.CosmosDb
{
public class CosmosCommonTests
{
    [Fact]
    public void CreateContainerCallState_DoesNotThrow_WhenDatabaseNewClientDisposed()
    {
        var container = new ContainerStub
        {
            Id = "container",
            Database = new DatabaseNewStub { Id = "db" }
        };

        Action act = () =>
        {
            CallTargetState state = CosmosCommon.CreateContainerCallStateExt(container, "SELECT 1");
            state.Scope?.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CreateContainerCallState_DoesNotThrow_WhenDatabaseOldClientDisposed()
    {
        var container = new ContainerStub
        {
            Id = "container",
            Database = new DatabaseOldStub { Id = "db" }
        };

        Action act = () =>
        {
            CallTargetState state = CosmosCommon.CreateContainerCallStateExt(container, "SELECT 1");
            state.Scope?.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CreateDatabaseCallState_DoesNotThrow_WhenClientDisposed_New()
    {
        var database = new DatabaseNewStub { Id = "db" };

        Action act = () =>
        {
            CallTargetState state = CosmosCommon.CreateDatabaseCallStateExt(database, "SELECT 1");
            state.Scope?.Dispose();
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void CreateDatabaseCallState_DoesNotThrow_WhenClientDisposed_Old()
    {
        var database = new DatabaseOldStub { Id = "db" };

        Action act = () =>
        {
            CallTargetState state = CosmosCommon.CreateDatabaseCallStateExt(database, "SELECT 1");
            state.Scope?.Dispose();
        };

        act.Should().NotThrow();
    }

    private class ContainerStub
    {
        public string Id
        {
            get;
            set;
        }

        // Must be object to match ContainerStruct.Database
        public object Database
        {
            get;
            set;
        }
    }

    private class DatabaseNewStub
    {
        public string Id
        {
            get;
            set;
        }

        // Simulate disposed client by throwing on getter access
        public ThrowingCosmosClient Client => throw new ObjectDisposedException("CosmosClient");
    }

    private class DatabaseOldStub
    {
        public string Id
        {
            get;
            set;
        }

        public CosmosClientContextStub ClientContext
        {
            get;
        } = new CosmosClientContextStub();
    }

    private class CosmosClientContextStub
    {
        // Simulate disposed client by throwing on getter access
        public ThrowingCosmosClient Client => throw new ObjectDisposedException("CosmosClient");
    }

    private class ThrowingCosmosClient
    {
        // Not expected to be reached in these tests, but provided for completeness
        public Uri Endpoint => new Uri("http://localhost");
    }
}
}
