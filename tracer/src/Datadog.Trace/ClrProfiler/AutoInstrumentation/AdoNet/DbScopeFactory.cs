// <copyright file="DbScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbScopeFactory<TCommand>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbScopeFactory<TCommand>));

        // these values are only valid in CreateDbCommandScope() if command type is TCommand
        private static readonly Type? _type;
        private static readonly string? _dbTypeName;
        private static readonly string? _operationName;
        private static readonly IntegrationInfo? _integrationInfo;

        static DbScopeFactory()
        {
            _type = typeof(TCommand);

            if (TryGetIntegrationDetails(_type, out var integrationId, out var dbTypeName))
            {
                // try to cache details for TCommand, but sometimes we will need a fallback
                // (e.g. if TCommand is DbCommand because we are instrumenting a method on the base type)
                _dbTypeName = dbTypeName;
                _operationName = $"{_dbTypeName}.query";
                _integrationInfo = IntegrationRegistry.GetIntegrationInfo(integrationId.ToString());
            }
        }

        public static Scope? CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            var commandType = command.GetType();

            if (commandType == _type && _integrationInfo != null && _dbTypeName != null)
            {
                // use the integration details cached in static fields if command type is TCommand
                return CreateDbCommandScope(tracer, command, _integrationInfo.Value, _dbTypeName);
            }

            // if command type is not TCommand, we are probably instrumenting a method
            // defined in a base class like DbCommand and we can't use the cached integration details
            if (TryGetIntegrationDetails(commandType, out var integrationId, out var dbType))
            {
                if (integrationId is not null && dbType is not null)
                {
                    var integration = IntegrationRegistry.GetIntegrationInfo(integrationId.ToString());
                    return CreateDbCommandScope(tracer, command, integration, dbType);
                }
            }

            // could not determine integration details from command type
            return null;
        }

        public static Scope? CreateDbCommandScope(Tracer tracer, IDbCommand command, IntegrationInfo integration, string dbType)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integration))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            try
            {
                Span? parent = tracer.ActiveScope?.Span;

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

                Scope scope = tracer.StartActiveWithTags(_operationName, tags: tags, serviceName: serviceName);
                scope.Span.AddTagsFromDbCommand(command);

                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
                return null;
            }
        }

        public static bool TryGetIntegrationDetails(
            Type commandType,
            [NotNullWhen(true)] out IntegrationIds? integrationId,
            [NotNullWhen(true)] out string? dbType)
        {
            string commandTypeFullName = commandType.FullName ?? string.Empty;

            if (commandTypeFullName.Length < 2)
            {
                // name is too short
                integrationId = null;
                dbType = null;
                return false;
            }

            switch (commandTypeFullName[0])
            {
                case 'S' when commandTypeFullName is "System.Data.SqlClient.SqlCommand":
                    integrationId = IntegrationIds.SqlClient;
                    dbType = DbType.SqlServer;
                    return true;
                case 'S' when commandTypeFullName is "System.Data.SQLite.SQLiteCommand": // note capitalization in SQLite
                    integrationId = IntegrationIds.Sqlite;
                    dbType = DbType.Sqlite;
                    return true;
                case 'M' when commandTypeFullName is "Microsoft.Data.SqlClient.SqlCommand":
                    integrationId = IntegrationIds.SqlClient;
                    dbType = DbType.SqlServer;
                    return true;
                case 'M' when commandTypeFullName is "Microsoft.Data.Sqlite.SqliteCommand": // note capitalization in Sqlite
                    integrationId = IntegrationIds.Sqlite;
                    dbType = DbType.Sqlite;
                    return true;
                case 'M' when commandTypeFullName[1] is 'y' &&
                              commandTypeFullName is "MySql.Data.MySqlClient.MySqlCommand" or "MySqlConnector.MySqlCommand":
                    integrationId = IntegrationIds.MySql;
                    dbType = DbType.MySql;
                    return true;
                case 'N' when commandTypeFullName is "Npgsql.NpgsqlCommand":
                    integrationId = IntegrationIds.Npgsql;
                    dbType = DbType.PostgreSql;
                    return true;
                case 'O' when commandTypeFullName is "Oracle.ManagedDataAccess.Client.OracleCommand" or "Oracle.DataAccess.Client.OracleCommand":
                    integrationId = IntegrationIds.Oracle;
                    dbType = DbType.Oracle;
                    return true;
            }

            integrationId = null;
            dbType = null;
            return false;
        }
    }
}
