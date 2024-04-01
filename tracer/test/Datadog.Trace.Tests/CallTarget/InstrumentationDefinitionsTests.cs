// <copyright file="InstrumentationDefinitionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using System.Data.Common;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Http.HttpClient.HttpClientHandler;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit;
using Datadog.Trace.Configuration;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.CallTarget
{
    public class InstrumentationDefinitionsTests
    {
        [Fact]
        public void CanGetIntegrationIdFromInstrumentAttribute()
        {
            var integrationType = typeof(HttpClientHandlerIntegration).FullName;
            var targetType = typeof(System.Net.Http.HttpClientHandler);

            var info = InstrumentationDefinitions.GetIntegrationId(integrationType, targetType);

            info.Should().NotBeNull();
            info.Value.Should().Be(IntegrationId.HttpMessageHandler);
        }

        [Fact]
        public void CanGetIntegrationIdFromInstrumentAttributeWithMultipleAssemblyNames()
        {
            var integrationType = typeof(XUnitTestInvokerRunAsyncIntegration).FullName;
            var targetType = typeof(Xunit.Sdk.TestInvoker<>);

            var info = InstrumentationDefinitions.GetIntegrationId(integrationType, targetType);

            info.Should().NotBeNull();
            info.Value.Should().Be(IntegrationId.XUnit);
        }

        [Fact]
        public void CanGetIntegrationIdFromInstrumentAttributeWithMultipleTypeNames()
        {
            var integrationType = typeof(IWireProtocol_Generic_Execute_Integration).FullName;

            // target type doesn't matter in this case
            var info = InstrumentationDefinitions.GetIntegrationId(integrationType, null);

            info.Should().NotBeNull();
            info.Value.Should().Be(IntegrationId.MongoDb);
        }

        [Fact]
        public void CanGetIntegrationIdForAdoNetAttribute()
        {
            var types = new[]
            {
                (typeof(System.Data.SqlClient.SqlCommand), (int)IntegrationId.SqlClient),
                (typeof(System.Data.SQLite.SQLiteCommand), (int)IntegrationId.Sqlite),
            };

            foreach (var (targetType, expected) in types)
            {
                var integrationType = typeof(CommandExecuteNonQueryIntegration).FullName;

                var info = InstrumentationDefinitions.GetIntegrationId(integrationType, targetType);

                info.Should().NotBeNull();
                info.Value.Should().Be((IntegrationId)expected);
            }
        }

        [Fact]
        public void CanGetIntegrationIdFromAssemblyLevelInstrumentAttribute_IntegrationTypeIsDerived()
        {
            var integrationType = typeof(CommandExecuteNonQueryIntegration).FullName;
            var targetType = typeof(FakeCommand);

            var info = InstrumentationDefinitions.GetIntegrationId(integrationType, targetType);

            info.Should().NotBeNull();
            info.Value.Should().Be(IntegrationId.AdoNet);
        }

        [Fact]
        public void ForUnknownIntegrationType_ReturnsNull()
        {
            var integrationType = typeof(InstrumentationDefinitionsTests).FullName;
            var targetType = typeof(FakeCommand);

            var info = InstrumentationDefinitions.GetIntegrationId(integrationType, targetType);

            info.Should().BeNull();
        }

#nullable disable
        public class FakeCommand : DbCommand
        {
            public override bool DesignTimeVisible { get; set; }

            public override string CommandText { get; set; }

            public override int CommandTimeout { get; set; }

            public override CommandType CommandType { get; set; }

            public override UpdateRowSource UpdatedRowSource { get; set; }

            protected override DbConnection DbConnection { get; set; }

            protected override DbParameterCollection DbParameterCollection { get; }

            protected override DbTransaction DbTransaction { get; set; }

            public override void Prepare()
            {
            }

            public override void Cancel()
            {
            }

            public override int ExecuteNonQuery() => 0;

            public override object ExecuteScalar() => null;

            protected override DbParameter CreateDbParameter() => default!;

            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => null;
        }
    }
}
