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
            yield return new object[] { new System.Data.SqlClient.SqlCommand(),    nameof(IntegrationIds.SqlClient), DbType.SqlServer  };
            yield return new object[] { new Microsoft.Data.SqlClient.SqlCommand(), nameof(IntegrationIds.SqlClient), DbType.SqlServer  };
            yield return new object[] { new MySql.Data.MySqlClient.MySqlCommand(), nameof(IntegrationIds.MySql),     DbType.MySql      };
            yield return new object[] { new Npgsql.NpgsqlCommand(),                nameof(IntegrationIds.Npgsql),    DbType.PostgreSql };
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
        [InlineData("System.Data.SqlClient.SqlCommand",              true,  IntegrationIds.SqlClient, DbType.SqlServer)]
        [InlineData("Microsoft.Data.SqlClient.SqlCommand",           true,  IntegrationIds.SqlClient, DbType.SqlServer)]
        [InlineData("Npgsql.NpgsqlCommand",                          true,  IntegrationIds.Npgsql,    DbType.PostgreSql)]
        [InlineData("MySql.Data.MySqlClient.MySqlCommand",           true,  IntegrationIds.MySql,     DbType.MySql)]
        [InlineData("MySqlConnector.MySqlCommand",                   true,  IntegrationIds.MySql,     DbType.MySql)]
        [InlineData("Oracle.ManagedDataAccess.Client.OracleCommand", true,  IntegrationIds.Oracle,    DbType.Oracle)]
        [InlineData("Oracle.DataAccess.Client.OracleCommand",        true,  IntegrationIds.Oracle,    DbType.Oracle)]
        [InlineData("System.Data.SQLite.SQLiteCommand",              true,  IntegrationIds.Sqlite,    DbType.Sqlite)]
        [InlineData("Microsoft.Data.Sqlite.SqliteCommand",           true,  IntegrationIds.Sqlite,    DbType.Sqlite)]
        [InlineData("UnknownCommand",                                false, null,                     null)]
        internal void TryGetIntegrationDetails_CorrectNameGenerated(string commandTypeFullName, bool expectedReturn, IntegrationIds? expectedIntegrationId, string expectedDbType)
        {
            bool actualReturn = DbScopeFactory.TryGetIntegrationDetails(commandTypeFullName, out var actualIntegrationId, out var actualDbType);
            Assert.Equal(expectedReturn, actualReturn);
            Assert.Equal(expectedIntegrationId, actualIntegrationId);
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
