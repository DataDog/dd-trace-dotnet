// <copyright file="DbScopeFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using DbType = Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.DbType;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    public class DbScopeFactoryTests
    {
        private const string DbmCommandText = "SELECT 1";

        public static TheoryData<Type, string, string> GetDbCommands() => new()
        {
            { typeof(System.Data.SqlClient.SqlCommand),              nameof(IntegrationId.SqlClient), DbType.SqlServer  },
            { typeof(Microsoft.Data.SqlClient.SqlCommand),           nameof(IntegrationId.SqlClient), DbType.SqlServer  },
            { typeof(MySql.Data.MySqlClient.MySqlCommand),           nameof(IntegrationId.MySql),     DbType.MySql      },
            { typeof(MySqlConnector.MySqlCommand),                   nameof(IntegrationId.MySql),     DbType.MySql      },
            { typeof(Npgsql.NpgsqlCommand),                          nameof(IntegrationId.Npgsql),    DbType.PostgreSql },
            { typeof(Microsoft.Data.Sqlite.SqliteCommand),           nameof(IntegrationId.Sqlite),    DbType.Sqlite     },
            { typeof(System.Data.SQLite.SQLiteCommand),              nameof(IntegrationId.Sqlite),    DbType.Sqlite     },
            { typeof(Oracle.ManagedDataAccess.Client.OracleCommand), nameof(IntegrationId.Oracle),    DbType.Oracle     },
            { typeof(Oracle.DataAccess.Client.OracleCommand),        nameof(IntegrationId.Oracle),    DbType.Oracle     },
        };

        public static TheoryData<Type> GetDbmCommands()
            => new()
            {
                typeof(System.Data.SqlClient.SqlCommand),
                typeof(Microsoft.Data.SqlClient.SqlCommand),
                typeof(MySql.Data.MySqlClient.MySqlCommand),
                typeof(MySqlConnector.MySqlCommand),
                typeof(Npgsql.NpgsqlCommand),
                // We don't support SqlLite or Oracle in DBM
                // "Microsoft.Data.Sqlite.SqliteCommand",
                // "System.Data.SQLite.SQLiteCommand",
                // "Oracle.ManagedDataAccess.Client.OracleCommand",
                // "Oracle.DataAccess.Client.OracleCommand",
            };

        public static IEnumerable<object[]> GetEnabledDbmData()
            => from command in GetDbmCommands()
               from dbm in new[] { "service", "full" }
               select new[] { command[0], dbm };

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public async Task CreateDbCommandScope_ReturnsScopeForEnabledIntegration(Type commandType, string integrationName, string dbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            await using var tracer = CreateTracerWithIntegrationEnabled(integrationName, enabled: true);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.NotNull(scope);
            Assert.Equal(dbType, scope.Span.GetTag(Tags.DbType));
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public async Task CreateDbCommandScope_ReturnNullForDisabledIntegration(Type commandType, string integrationName, string dbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            // HACK: avoid analyzer warning about not using arguments
            _ = dbType;

            await using var tracer = CreateTracerWithIntegrationEnabled(integrationName, enabled: false);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.Null(scope);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public async Task CreateDbCommandScope_ReturnNullForAdoNetDisabledIntegration(Type commandType, string integrationName, string dbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            // HACK: avoid analyzer warning about not using arguments
            _ = dbType;

            var tracerSettings = new TracerSettings();
            tracerSettings.Integrations[integrationName].Enabled = true;
            tracerSettings.Integrations[nameof(IntegrationId.AdoNet)].Enabled = false;
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.Null(scope);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public async Task CreateDbCommandScope_UsesReplacementServiceNameWhenProvided(Type commandType, string integrationName, string dbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            // HACK: avoid analyzer warning about not using arguments
            _ = integrationName;

            // Set up tracer
            var collection = new NameValueCollection { { ConfigurationKeys.ServiceNameMappings, $"{dbType}:my-custom-type" } };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.Equal("my-custom-type", scope.Span.ServiceName);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        public async Task CreateDbCommandScope_IgnoresReplacementServiceNameWhenNotProvided(Type commandType, string integrationName, string dbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            // HACK: avoid analyzer warning about not using arguments
            _ = dbType;
            _ = integrationName;

            // Set up tracer
            var collection = new NameValueCollection { { ConfigurationKeys.ServiceNameMappings, "something:my-custom-type" } };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source);
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            // Create scope
            using var scope = CreateDbCommandScope(tracer, command);
            Assert.NotEqual("my-custom-type", scope.Span.ServiceName);
        }

        [Theory]
        [MemberData(nameof(GetEnabledDbmData))]
        public async Task CreateDbCommandScope_InjectsDbmWhenEnabled(Type commandType, string dbmMode)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            var collection = new NameValueCollection
            {
                { ConfigurationKeys.DbmPropagationMode, dbmMode }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            // Should have injected the data
            command.CommandText.Should().NotBe(DbmCommandText).And.Contain(DbmCommandText);
        }

        [Theory]
        [MemberData(nameof(GetEnabledDbmData))]
        public async Task CreateDbCommandScope_OnlyInjectsDbmOnceWhenCommandIsReused(Type commandType, string dbmMode)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            var collection = new NameValueCollection
            {
                { ConfigurationKeys.DbmPropagationMode, dbmMode }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using (var scope = CreateDbCommandScope(tracer, command))
            {
                scope.Should().NotBeNull();
            }

            // Should have injected the data once
            var injectedTest = command.CommandText.Should()
                                     .NotBe(DbmCommandText)
                                     .And.Contain(DbmCommandText)
                                     .And.Subject;

            // second attempt should still have the same data
            using (var scope = CreateDbCommandScope(tracer, command))
            {
                scope.Should().NotBeNull();
            }

            command.CommandText.Should().Be(injectedTest);
        }

        [Theory]
        [MemberData(nameof(GetEnabledDbmData))]
        public async Task CreateDbCommandScope_DoesNotInjectDbmIntoStoredProcedures(Type commandType, string dbmMode)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;
            command.CommandType = CommandType.StoredProcedure;

            var collection = new NameValueCollection
            {
                { ConfigurationKeys.DbmPropagationMode, dbmMode }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            // Should not have injected the data
            command.CommandText.Should().Be(DbmCommandText);
        }

        [Theory]
        [MemberData(nameof(GetDbmCommands))]
        public async Task CreateDbCommandScope_DoesNotInjectDbmWhenDisabled(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            var collection = new NameValueCollection
            {
                { ConfigurationKeys.DbmPropagationMode, "disabled" }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            // Should not have injected the data
            command.CommandText.Should().Be(DbmCommandText);
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        internal void TryGetIntegrationDetails_CorrectNameGenerated(Type commandType, string expectedIntegrationName, string expectedDbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

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

        private static TracerHelper.ScopedTracer CreateTracerWithIntegrationEnabled(string integrationName, bool enabled)
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
