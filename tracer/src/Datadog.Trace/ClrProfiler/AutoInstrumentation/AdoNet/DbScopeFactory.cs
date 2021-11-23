// <copyright file="DbScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbScopeFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbScopeFactory));

        private static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command, IntegrationId integrationId, string dbType, string operationName)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope scope = null;

            try
            {
                ISpan parent = tracer.ActiveScope?.Span;

                if (parent is { Type: SpanTypes.Sql } &&
                    parent.GetTag(Tags.DbType) == dbType &&
                    parent.ResourceName == command.CommandText)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. ExecuteReader() -> ExecuteReader(commandBehavior)
                    return null;
                }

                string serviceName = tracer.Settings.GetServiceName(tracer, dbType);

                var tags = new SqlTags
                           {
                               DbType = dbType,
                               InstrumentationName = integrationId.ToString()
                           };

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);

                scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);
                scope.Span.AddTagsFromDbCommand(command);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        public static bool TryGetIntegrationDetails(
            string commandTypeFullName,
            [NotNullWhen(true)] out IntegrationId? integrationId,
            [NotNullWhen(true)] out string dbType)
        {
            // TODO: optimize this switch
            switch (commandTypeFullName)
            {
                case "System.Data.SqlClient.SqlCommand" or "Microsoft.Data.SqlClient.SqlCommand":
                    integrationId = IntegrationId.SqlClient;
                    dbType = DbType.SqlServer;
                    return true;
                case "Npgsql.NpgsqlCommand":
                    integrationId = IntegrationId.Npgsql;
                    dbType = DbType.PostgreSql;
                    return true;
                case "MySql.Data.MySqlClient.MySqlCommand" or "MySqlConnector.MySqlCommand":
                    integrationId = IntegrationId.MySql;
                    dbType = DbType.MySql;
                    return true;
                case "Oracle.ManagedDataAccess.Client.OracleCommand" or "Oracle.DataAccess.Client.OracleCommand":
                    integrationId = IntegrationId.Oracle;
                    dbType = DbType.Oracle;
                    return true;
                case "System.Data.SQLite.SQLiteCommand" or "Microsoft.Data.Sqlite.SqliteCommand":
                    // note capitalization in SQLite/Sqlite
                    integrationId = IntegrationId.Sqlite;
                    dbType = DbType.Sqlite;
                    return true;
                default:
                    integrationId = null;
                    dbType = null;
                    return false;
            }
        }

        public static class Cache<TCommand>
        {
            // ReSharper disable StaticMemberInGenericType
            // Static fields used intentionally to cache a different set of values for each TCommand.
            private static readonly Type CommandType;
            private static readonly string DbTypeName;
            private static readonly string OperationName;
            private static readonly IntegrationId IntegrationId;
            // ReSharper restore StaticMemberInGenericType

            static Cache()
            {
                var commandType = typeof(TCommand);

                if (TryGetIntegrationDetails(commandType.FullName, out var integrationId, out var dbTypeName))
                {
                    // cache values for this TCommand type
                    CommandType = commandType;
                    DbTypeName = dbTypeName;
                    OperationName = $"{DbTypeName}.query";
                    IntegrationId = integrationId.Value;
                }
            }

            public static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
            {
                var commandType = command.GetType();

                if (commandType == CommandType)
                {
                    // use the cached values if command.GetType() == typeof(TCommand)
                    return DbScopeFactory.CreateDbCommandScope(tracer, command, IntegrationId, DbTypeName, OperationName);
                }

                // if command.GetType() != typeof(TCommand), we are probably instrumenting a method
                // defined in a base class like DbCommand and we can't use the cached values
                if (TryGetIntegrationDetails(commandType.FullName, out var integrationId, out var dbTypeName))
                {
                    var operationName = $"{dbTypeName}.query";
                    return DbScopeFactory.CreateDbCommandScope(tracer, command, integrationId.Value, dbTypeName, operationName);
                }

                return null;
            }
        }
    }
}
