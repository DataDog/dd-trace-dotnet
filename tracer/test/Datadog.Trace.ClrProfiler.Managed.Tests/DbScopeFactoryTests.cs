// <copyright file="DbScopeFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using DbType = Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.DbType;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class DbScopeFactoryTests
    {
        public static IEnumerable<object[]> GetDbCommands()
        {
            yield return new object[] { new System.Data.SqlClient.SqlCommand(),              nameof(IntegrationId.SqlClient), DbType.SqlServer  };
            yield return new object[] { new Microsoft.Data.SqlClient.SqlCommand(),           nameof(IntegrationId.SqlClient), DbType.SqlServer  };
            yield return new object[] { new MySql.Data.MySqlClient.MySqlCommand(),           nameof(IntegrationId.MySql),     DbType.MySql      };
            yield return new object[] { new MySqlConnector.MySqlCommand(),                   nameof(IntegrationId.MySql),     DbType.MySql      };
            yield return new object[] { new Npgsql.NpgsqlCommand(),                          nameof(IntegrationId.Npgsql),    DbType.PostgreSql };
            yield return new object[] { new Microsoft.Data.Sqlite.SqliteCommand(),           nameof(IntegrationId.Sqlite),    DbType.Sqlite     };
            yield return new object[] { new System.Data.SQLite.SQLiteCommand(),              nameof(IntegrationId.Sqlite),    DbType.Sqlite     };
            yield return new object[] { new Oracle.ManagedDataAccess.Client.OracleCommand(), nameof(IntegrationId.Oracle),    DbType.Oracle     };
            yield return new object[] { new Oracle.DataAccess.Client.OracleCommand(),        nameof(IntegrationId.Oracle),    DbType.Oracle     };
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public void CreateDbCommandScope_ReturnsScopeForEnabledIntegration(IDbCommand command, string integrationName, string dbType)
        {
            var tracer = CreateTracerWithIntegrationEnabled(integrationName, enabled: true);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.NotNull(scope);
            Assert.Equal(dbType, scope.Span.GetTag(Tags.DbType));
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public void CreateDbCommandScope_ReturnNullForDisabledIntegration(IDbCommand command, string integrationName, string dbType)
        {
            // HACK: avoid analyzer warning about not using arguments
            _ = dbType;

            var tracer = CreateTracerWithIntegrationEnabled(integrationName, enabled: false);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.Null(scope);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public void CreateDbCommandScope_ReturnNullForAdoNetDisabledIntegration(IDbCommand command, string integrationName, string dbType)
        {
            // HACK: avoid analyzer warning about not using arguments
            _ = dbType;

            var tracerSettings = new TracerSettings();
            tracerSettings.Integrations[integrationName].Enabled = true;
            tracerSettings.Integrations[nameof(IntegrationId.AdoNet)].Enabled = false;
            var tracer = TracerHelper.Create(tracerSettings);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.Null(scope);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public void CreateDbCommandScope_UsesReplacementServiceNameWhenProvided(IDbCommand command, string integrationName, string dbType)
        {
            // HACK: avoid analyzer warning about not using arguments
            _ = integrationName;

            // Set up tracer
            var collection = new NameValueCollection { { ConfigurationKeys.ServiceNameMappings, $"{dbType}:my-custom-type" } };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = TracerHelper.Create(tracerSettings);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.Equal("my-custom-type", scope.Span.ServiceName);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public void CreateDbCommandScope_IgnoresReplacementServiceNameWhenNotProvided(IDbCommand command, string integrationName, string dbType)
        {
            // HACK: avoid analyzer warning about not using arguments
            _ = dbType;
            _ = integrationName;

            // Set up tracer
            var collection = new NameValueCollection { { ConfigurationKeys.ServiceNameMappings, "something:my-custom-type" } };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            var tracer = TracerHelper.Create(tracerSettings);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.NotEqual("my-custom-type", scope.Span.ServiceName);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        internal void TryGetIntegrationDetails_CorrectNameGenerated(IDbCommand command, string expectedIntegrationName, string expectedDbType)
        {
            bool result = DbScopeFactory.TryGetIntegrationDetails(command.GetType().FullName, out var actualIntegrationId, out var actualDbType);
            Assert.True(result);
            Assert.Equal(expectedIntegrationName, actualIntegrationId.ToString());
            Assert.Equal(expectedDbType, actualDbType);
        }

        [Fact]
        internal void TryGetIntegrationDetails_FailsForKnownCommandType()
        {
            bool result = DbScopeFactory.TryGetIntegrationDetails("InterceptableDbCommand", out var actualIntegrationId, out var actualDbType);
            Assert.False(result);
            Assert.False(actualIntegrationId.HasValue);
            Assert.Null(actualDbType);

            bool result2 = DbScopeFactory.TryGetIntegrationDetails("ProfiledDbCommand", out var actualIntegrationId2, out var actualDbType2);
            Assert.False(result2);
            Assert.False(actualIntegrationId2.HasValue);
            Assert.Null(actualDbType2);
        }

        [Theory]
        [InlineData("System.Data.SqlClient.SqlCommand", "SqlClient", "sql-server")]
        [InlineData("MySql.Data.MySqlClient.MySqlCommand", "MySql", "mysql")]
        [InlineData("Npgsql.NpgsqlCommand", "Npgsql", "postgres")]
        [InlineData("ProfiledDbCommand", null, null)]
        [InlineData("ExampleCommand", "AdoNet", "example")]
        [InlineData("Example", "AdoNet", "example")]
        [InlineData("Command", "AdoNet", "command")]
        [InlineData("Custom.DB.Command", "AdoNet", "db")]
        internal void TryGetIntegrationDetails_CustomCommandType(string commandTypeFullName, string integrationId, string expectedDbType)
        {
            DbScopeFactory.TryGetIntegrationDetails(commandTypeFullName, out var actualIntegrationId, out var actualDbType);
            Assert.Equal(integrationId, actualIntegrationId?.ToString());
            Assert.Equal(expectedDbType, actualDbType);
        }

        private static Tracer CreateTracerWithIntegrationEnabled(string integrationName, bool enabled)
        {
            // Set up tracer
            var tracerSettings = new TracerSettings();
            tracerSettings.Integrations[integrationName].Enabled = enabled;
            return TracerHelper.Create(tracerSettings);
        }

        private static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            var methodName = nameof(DbScopeFactory.Cache<object>.CreateDbCommandScope);
            var arguments = new object[] { tracer, command };

            return (Scope)typeof(DbScopeFactory.Cache<>).MakeGenericType(command.GetType())
                                                        .GetMethod(methodName)
                                                       ?.Invoke(null, arguments);
        }
    }
}
