// <copyright file="DbScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbScopeFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbScopeFactory));

        private static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command, IntegrationInfo integration, string dbType, string operationName)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integration))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.ActiveScope?.Span;

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
                               InstrumentationName = integration.Name
                           };

                tags.SetAnalyticsSampleRate(integration, tracer.Settings, enabledWithGlobalSetting: false);

                scope = tracer.StartActiveWithTags(operationName, tags: tags, serviceName: serviceName);
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
            out IntegrationIds? integrationId,
            out string dbType)
        {
            // TODO: optimize this switch
            switch (commandTypeFullName)
            {
                case "System.Data.SqlClient.SqlCommand" or "Microsoft.Data.SqlClient.SqlCommand":
                    integrationId = IntegrationIds.SqlClient;
                    dbType = DbType.SqlServer;
                    return true;
                case "Npgsql.NpgsqlCommand":
                    integrationId = IntegrationIds.Npgsql;
                    dbType = DbType.PostgreSql;
                    return true;
                case "MySql.Data.MySqlClient.MySqlCommand" or "MySqlConnector.MySqlCommand":
                    integrationId = IntegrationIds.MySql;
                    dbType = DbType.MySql;
                    return true;
                case "Oracle.ManagedDataAccess.Client.OracleCommand" or "Oracle.DataAccess.Client.OracleCommand":
                    integrationId = IntegrationIds.Oracle;
                    dbType = DbType.Oracle;
                    return true;
                case "System.Data.SQLite.SQLiteCommand" or "Microsoft.Data.Sqlite.SqliteCommand":
                    // note capitalization in SQLite/Sqlite
                    integrationId = IntegrationIds.Sqlite;
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
            private static readonly Type _commandType;
            private static readonly string _dbTypeName;
            private static readonly string _operationName;
            private static readonly IntegrationInfo _integrationInfo;

            static Cache()
            {
                var commandType = typeof(TCommand);

                if (TryGetIntegrationDetails(commandType.FullName, out var integrationId, out var dbTypeName))
                {
                    // cache values for this TCommand type
                    _commandType = commandType;
                    _dbTypeName = dbTypeName;
                    _operationName = $"{_dbTypeName}.query";
                    _integrationInfo = IntegrationRegistry.GetIntegrationInfo(integrationId.ToString());
                }
            }

            public static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
            {
                var commandType = command.GetType();

                if (commandType == _commandType)
                {
                    // use the cached values if command.GetType() == typeof(TCommand)
                    return DbScopeFactory.CreateDbCommandScope(tracer, command, _integrationInfo, _dbTypeName, _operationName);
                }

                // if command.GetType() != typeof(TCommand), we are probably instrumenting a method
                // defined in a base class like DbCommand and we can't use the cached values
                if (TryGetIntegrationDetails(command.GetType().FullName, out var integrationId, out var dbTypeName))
                {
                    var integration = IntegrationRegistry.GetIntegrationInfo(integrationId.ToString());
                    var operationName = $"{dbTypeName}.query";
                    return DbScopeFactory.CreateDbCommandScope(tracer, command, integration, dbTypeName, operationName);
                }

                return null;
            }
        }
    }
}
