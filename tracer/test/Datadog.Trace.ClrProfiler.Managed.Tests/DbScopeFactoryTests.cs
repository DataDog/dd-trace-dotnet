// <copyright file="DbScopeFactoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet;
using Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AdoNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources.Telemetry;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Xunit;
using DbType = Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.DbType;

namespace Datadog.Trace.ClrProfiler.Managed.Tests
{
    [Collection("DbScopeFactoryTests")]
    [TracerRestorer]
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
            => from command in (IEnumerable<object[]>)GetDbmCommands()
               from dbm in new[] { "service", "full" }
               from storedProcInject in new[] { false, true }
               select new[] { command[0], dbm, storedProcInject };

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

            var tracerSettings = TracerSettings.Create(new()
            {
                { string.Format(IntegrationSettings.IntegrationEnabledKey, integrationName), "true" },
                { string.Format(IntegrationSettings.IntegrationEnabledKey, nameof(IntegrationId.AdoNet)), "false" },
            });
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
        public async Task CreateDbCommandScope_HasBaseHashWhenConfigured(Type commandType, string dbmMode, bool hashPropagationEnabled)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            var tracerSettings = TracerSettings.Create(new Dictionary<string, object>
            {
                { ConfigurationKeys.DbmInjectSqlBasehash, hashPropagationEnabled.ToString() },
                { ConfigurationKeys.DbmPropagationMode, dbmMode }
            });
            await using var tracer = TracerHelper.Create(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command, "my_hash");

            scope.Should().NotBeNull();
            if (hashPropagationEnabled)
            {
                scope.Span.GetTag(Tags.BaseHash).Should().Be("my_hash");
                command.CommandText.Should().Contain("ddsh='my_hash'");
            }
            else
            {
                scope.Span.GetTag(Tags.BaseHash).Should().BeNull();
                command.CommandText.Should().NotContain("ddsh=");
            }
        }

        [Theory]
        [MemberData(nameof(GetEnabledDbmData))]
        public async Task CreateDbCommandScope_InjectsDbmWhenEnabled(Type commandType, string dbmMode, bool storedProcInject)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, dbmMode
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, storedProcInject.ToString()
                }
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
        public async Task CreateDbCommandScope_OnlyInjectsDbmOnceWhenCommandIsReused(Type commandType, string dbmMode, bool storedProcInject)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, dbmMode
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, storedProcInject.ToString()
                }
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

        [Fact]
        public async Task CreateDbCommandScope_DetectsCommandIsReusedOnAppend()
        {
            var command = (IDbCommand)Activator.CreateInstance(typeof(Npgsql.NpgsqlCommand))!;
            // adding a query plan hint to trigger
            command.CommandText = "/*+ IndexScan(a) */ " + DbmCommandText;

            IConfigurationSource source = new NameValueConfigurationSource(new NameValueCollection { { ConfigurationKeys.DbmPropagationMode, "service" } });
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using (var scope = CreateDbCommandScope(tracer, command))
            {
                scope.Should().NotBeNull();
            }

            // Should have injected the data once, not prepended
            var injectedTest = command.CommandText.Should()
                                      .NotBe(DbmCommandText)
                                      .And.NotEndWith(DbmCommandText)
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
        public async Task CreateDbCommandScope_DoesNotInjectDbmIntoStoredProcedures_ExceptForSqlCommand(Type commandType, string dbmMode, bool storedProcInject)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;
            command.CommandType = CommandType.StoredProcedure;

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, dbmMode
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, storedProcInject.ToString()
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            if (storedProcInject && (commandType == typeof(System.Data.SqlClient.SqlCommand) || commandType == typeof(Microsoft.Data.SqlClient.SqlCommand)))
            {
                // should have injected data
                // command text should be exec
                command.CommandText.Should().NotBe(DbmCommandText).And.Contain($"EXEC [{DbmCommandText}]");
                command.CommandText.Should().Contain("/*dddbs"); // check for the dbm comment this isn't all of it but good enough
            }
            else
            {
                // should not have injected data - command text should not change
                command.CommandText.Should().Be(DbmCommandText);
            }
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

            // should not have injected data - command text should not change
            command.CommandText.Should().Be(DbmCommandText);
        }

        // TODO; can probably clean these Stored Procedures up at some point, I just copy pasted
        // TODO: can't get [InlineData(typeof(System.Data.SqlClient.SqlCommand))] to work with the MockParameters
        [Theory]
        [InlineData(typeof(Microsoft.Data.SqlClient.SqlCommand))]
        [InlineData(typeof(System.Data.SqlClient.SqlCommand))]
        public async Task StoredProc_Parameterless_CorrectlyTransformedIntoExec(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            command.CommandText = "dbo.Parameterless";
            command.CommandType = CommandType.StoredProcedure;

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, "full"
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, "true"
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            command.CommandType.Should().Be(CommandType.Text);
            command.CommandText.Should().StartWith("EXEC [dbo].[Parameterless] ");
            command.CommandText.Should().Contain("/*dddbs=");
        }

        [Theory]
        [InlineData(typeof(Microsoft.Data.SqlClient.SqlCommand))]
        [InlineData(typeof(System.Data.SqlClient.SqlCommand))]
        public async Task StoredProc_SingleInputParameter_CorrectlyTransformedIntoExec(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            command.CommandText = "dbo.SingleParameter";
            command.CommandType = CommandType.StoredProcedure;

#if NETFRAMEWORK
            var parameter = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
#else
            var parameter = new MockDbParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
#endif
            command.Parameters.Add(parameter);

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, "full"
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, "true"
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            command.CommandType.Should().Be(CommandType.Text);
            command.CommandText.Should().StartWith("EXEC [dbo].[SingleParameter] @Id=@Id ");
            command.CommandText.Should().Contain("/*dddbs=");
        }

        [Theory]
        [InlineData(typeof(Microsoft.Data.SqlClient.SqlCommand))]
        [InlineData(typeof(System.Data.SqlClient.SqlCommand))]
        public async Task StoredProc_MultipleInputParameters_IsNotModified(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            command.CommandText = "dbo.MultiParameter";
            command.CommandType = CommandType.StoredProcedure;

#if NETFRAMEWORK
            var parameter = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
            command.Parameters.Add(parameter);

            var parameter2 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@SomeOtherId",
                Value = 55
            };
#else
            var parameter = new MockDbParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
            command.Parameters.Add(parameter);

            var parameter2 = new MockDbParameter
            {
                ParameterName = "@SomeOtherId",
                Value = 55
            };
#endif
            command.Parameters.Add(parameter2);

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, "full"
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, "true"
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            command.CommandType.Should().Be(CommandType.Text);
            command.CommandText.Should().StartWith("EXEC [dbo].[MultiParameter] @Id=@Id, @SomeOtherId=@SomeOtherId ");
            command.CommandText.Should().Contain("/*dddbs=");
        }

        [Theory]
        [InlineData(typeof(Microsoft.Data.SqlClient.SqlCommand))]
        public async Task StoredProc_OutputParameter_IsNotModified(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            command.CommandText = "dbo.OutputParameter";
            command.CommandType = CommandType.StoredProcedure;

#if NETFRAMEWORK
            var parameter = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
            command.Parameters.Add(parameter);

            var parameter2 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@SomeOtherId",
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(parameter2);

            var parameter3 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@SomeOtherIdFoo",
                Direction = ParameterDirection.InputOutput,
                Value = 10
            };
            command.Parameters.Add(parameter3);
#else
            var parameter = new MockDbParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
            command.Parameters.Add(parameter);

            var parameter2 = new MockDbParameter
            {
                ParameterName = "@SomeOtherId",
                Direction = ParameterDirection.Output
            };
            command.Parameters.Add(parameter2);

            var parameter3 = new MockDbParameter
            {
                ParameterName = "@SomeOtherIdFoo",
                Direction = ParameterDirection.InputOutput,
                Value = 10
            };
            command.Parameters.Add(parameter3);
#endif

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, "full"
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, "true"
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            command.CommandType.Should().Be(CommandType.StoredProcedure);
            command.CommandText.Should().Be("dbo.OutputParameter");
        }

        [Theory]
        [InlineData(typeof(Microsoft.Data.SqlClient.SqlCommand))]
        public async Task StoredProc_ReturnParameter_IsNotModified(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType);
            command.CommandText = "dbo.ReturnParam";
            command.CommandType = CommandType.StoredProcedure;

#if NETFRAMEWORK
            var parameter = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
            command.Parameters.Add(parameter);

            var parameter2 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@ReturnValue",
                Direction = ParameterDirection.ReturnValue
            };
            command.Parameters.Add(parameter2);
#else
            var parameter = new MockDbParameter
            {
                ParameterName = "@Id",
                Value = 5
            };
            command.Parameters.Add(parameter);

            var parameter2 = new MockDbParameter
            {
                ParameterName = "@ReturnValue",
                Direction = ParameterDirection.ReturnValue
            };
            command.Parameters.Add(parameter2);
#endif

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, "full"
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, "true"
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            command.CommandType.Should().Be(CommandType.StoredProcedure);
            command.CommandText.Should().Be("dbo.ReturnParam");
        }

        [Theory]
        [InlineData(typeof(Microsoft.Data.SqlClient.SqlCommand))]
        public async Task StoredProc_ComplexCase_MultipleParamsOfVariousTypes_IsNotModified(Type commandType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = "dbo.ComplexProcedure";
            command.CommandType = CommandType.StoredProcedure;

#if NETFRAMEWORK
            // Add various parameters
            var parameter1 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@InputParam",
                Value = "Input Value"
            };
            command.Parameters.Add(parameter1);

            var parameter2 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@OutputParam",
                Direction = ParameterDirection.Output,
                Size = 100
            };
            command.Parameters.Add(parameter2);

            var parameter3 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@InOutParam",
                Direction = ParameterDirection.InputOutput,
                Value = "Initial Value",
                Size = 100
            };
            command.Parameters.Add(parameter3);

            var parameter4 = new System.Data.SqlClient.SqlParameter
            {
                ParameterName = "@RetVal",
                Direction = ParameterDirection.ReturnValue
            };
            command.Parameters.Add(parameter4);
#else
            // Add various parameters
            var parameter1 = new MockDbParameter
            {
                ParameterName = "@InputParam",
                Value = "Input Value"
            };
            command.Parameters.Add(parameter1);

            var parameter2 = new MockDbParameter
            {
                ParameterName = "@OutputParam",
                Direction = ParameterDirection.Output,
                Size = 100
            };
            command.Parameters.Add(parameter2);

            var parameter3 = new MockDbParameter
            {
                ParameterName = "@InOutParam",
                Direction = ParameterDirection.InputOutput,
                Value = "Initial Value",
                Size = 100
            };
            command.Parameters.Add(parameter3);

            var parameter4 = new MockDbParameter
            {
                ParameterName = "@RetVal",
                Direction = ParameterDirection.ReturnValue
            };
            command.Parameters.Add(parameter4);
#endif

            var collection = new NameValueCollection
            {
                {
                    ConfigurationKeys.DbmPropagationMode, "full"
                },
                {
                    // these aren't stored proc so no changes expected
                    ConfigurationKeys.FeatureFlags.InjectContextIntoStoredProceduresEnabled, "true"
                }
            };
            IConfigurationSource source = new NameValueConfigurationSource(collection);
            var tracerSettings = new TracerSettings(source, NullConfigurationTelemetry.Instance, new OverrideErrorLog());
            await using var tracer = TracerHelper.CreateWithFakeAgent(tracerSettings);

            using var scope = CreateDbCommandScope(tracer, command);
            scope.Should().NotBeNull();

            // Should transform correctly with all appropriate parameters in correct format
            command.CommandType.Should().Be(CommandType.StoredProcedure);
            command.CommandText.Should().Be("dbo.ComplexProcedure");
        }

        [Theory]
        [MemberData(nameof(GetDbCommands))]
        internal void TryGetIntegrationDetails_CorrectNameGenerated(Type commandType, string expectedIntegrationName, string expectedDbType)
        {
            var command = (IDbCommand)Activator.CreateInstance(commandType)!;
            command.CommandText = DbmCommandText;

            bool result = DbScopeFactory.TryGetIntegrationDetails([], command.GetType().FullName, out var actualIntegrationId, out var actualDbType);
            Assert.True(result);
            Assert.Equal(expectedIntegrationName, actualIntegrationId.ToString());
            Assert.Equal(expectedDbType, actualDbType);
        }

        [Fact]
        internal void TryGetIntegrationDetails_FailsForKnownCommandType()
        {
            var defaultDisabledCommands = new TracerSettings().DisabledAdoNetCommandTypes;
            bool result = DbScopeFactory.TryGetIntegrationDetails(defaultDisabledCommands, "InterceptableDbCommand", out var actualIntegrationId, out var actualDbType);
            Assert.False(result);
            Assert.False(actualIntegrationId.HasValue);
            Assert.Null(actualDbType);

            bool result2 = DbScopeFactory.TryGetIntegrationDetails(defaultDisabledCommands, "ProfiledDbCommand", out var actualIntegrationId2, out var actualDbType2);
            Assert.False(result2);
            Assert.False(actualIntegrationId2.HasValue);
            Assert.Null(actualDbType2);
        }

        [Fact]
        internal void TryGetIntegrationDetails_FailsForKnownCommandTypes_AndUserDefined()
        {
            var disabledCommandTypes = TracerSettings.Create(new() { { ConfigurationKeys.DisabledAdoNetCommandTypes, "SomeFakeDbCommand" } }).DisabledAdoNetCommandTypes;
            bool result = DbScopeFactory.TryGetIntegrationDetails(disabledCommandTypes, "InterceptableDbCommand", out var actualIntegrationId, out var actualDbType);
            Assert.False(result);
            Assert.False(actualIntegrationId.HasValue);
            Assert.Null(actualDbType);

            bool result2 = DbScopeFactory.TryGetIntegrationDetails(disabledCommandTypes, "ProfiledDbCommand", out var actualIntegrationId2, out var actualDbType2);
            Assert.False(result2);
            Assert.False(actualIntegrationId2.HasValue);
            Assert.Null(actualDbType2);

            bool result3 = DbScopeFactory.TryGetIntegrationDetails(disabledCommandTypes, "SomeFakeDbCommand", out var actualIntegrationId3, out var actualDbType3);
            Assert.False(result3);
            Assert.False(actualIntegrationId3.HasValue);
            Assert.Null(actualDbType3);
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
            var defaultDisabledCommands = new TracerSettings().DisabledAdoNetCommandTypes;
            DbScopeFactory.TryGetIntegrationDetails(defaultDisabledCommands, commandTypeFullName, out var actualIntegrationId, out var actualDbType);
            Assert.Equal(integrationId, actualIntegrationId?.ToString());
            Assert.Equal(expectedDbType, actualDbType);
        }

        private static ScopedTracer CreateTracerWithIntegrationEnabled(string integrationName, bool enabled)
        {
            // Set up tracer
            var tracerSettings = TracerSettings.Create(new()
            {
                { string.Format(IntegrationSettings.IntegrationEnabledKey, integrationName), enabled },
            });
            return TracerHelper.Create(tracerSettings);
        }

        private static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command, string baseHash = null)
        {
            var methodName = nameof(DbScopeFactory.Cache<object>.CreateDbCommandScope);
            var arguments = new object[] { tracer, command, baseHash };

            return (Scope)typeof(DbScopeFactory.Cache<>).MakeGenericType(command.GetType())
                                                        .GetMethod(methodName)
                                                       ?.Invoke(null, arguments);
        }
    }
}
