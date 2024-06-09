// <copyright file="DbScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Datadog.Trace.Configuration;
using Datadog.Trace.DatabaseMonitoring;
using Datadog.Trace.Iast;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbScopeFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbScopeFactory));
        private static bool _dbCommandCachingLogged = false;

        private static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command, IntegrationId integrationId, string dbType, string operationName, string serviceName, ref DbCommandCache.TagsCacheItem tagsFromConnectionString)
        {
            if (!ShouldCreateScope(tracer, integrationId, dbType, command.CommandText))
            {
                return null;
            }

            Scope scope = null;

            try
            {
                SqlTags tags = tracer.CurrentTraceSettings.Schema.Database.CreateSqlTags();
                tags.DbType = dbType;
                tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);
                tags.DbName = tagsFromConnectionString.DbName;
                tags.DbUser = tagsFromConnectionString.DbUser;
                tags.OutHost = tagsFromConnectionString.OutHost;

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);
                scope.Span.ResourceName = command.CommandText;
                scope.Span.Type = SpanTypes.Sql;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);

                if (Iast.Iast.Instance.Settings.Enabled)
                {
                    IastModule.OnSqlQuery(command.CommandText, integrationId);
                }

                command.CommandText = InjectDbmInfo(tracer, command.CommandText, command.CommandType, integrationId, tagsFromConnectionString, scope, tags);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

#if NET6_0_OR_GREATER

        private static Scope CreateDbBatchScope(Tracer tracer, DbBatch batch, IntegrationId integrationId, string dbType, string operationName, string serviceName, ref DbCommandCache.TagsCacheItem tagsFromConnectionString)
        {
            if (batch.BatchCommands.Count == 0)
            {
                return null;
            }

            var firstCommandText = batch.BatchCommands[0].CommandText;
            if (!ShouldCreateScope(tracer, integrationId, dbType, firstCommandText))
            {
                return null;
            }

            Scope scope = null;

            try
            {
                var tags = tracer.CurrentTraceSettings.Schema.Database.CreateSqlTags();
                tags.DbType = dbType;
                tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);
                tags.DbName = tagsFromConnectionString.DbName;
                tags.DbUser = tagsFromConnectionString.DbUser;
                tags.OutHost = tagsFromConnectionString.OutHost;

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);
                scope.Span.ResourceName = firstCommandText; // TODO should we concatenate all commands ? With ; ?
                scope.Span.Type = SpanTypes.Sql;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);

                foreach (var command in batch.BatchCommands)
                {
                    if (Iast.Iast.Instance.Settings.Enabled)
                    {
                        IastModule.OnSqlQuery(command.CommandText, integrationId);
                    }

                    command.CommandText = InjectDbmInfo(tracer, command.CommandText, command.CommandType, integrationId, tagsFromConnectionString, scope, tags);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

#endif

        private static bool ShouldCreateScope(Tracer tracer, IntegrationId integrationId, string dbType, string commandText)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId) || !tracer.Settings.IsIntegrationEnabled(IntegrationId.AdoNet))
            {
                // integration disabled, don't create a scope, skip this span
                return false;
            }

            var parent = tracer.InternalActiveScope?.Span;
            if (parent is { Type: SpanTypes.Sql } &&
                HasDbType(parent, dbType) &&
                (parent.ResourceName == commandText || commandText.StartsWith(DatabaseMonitoringPropagator.DbmPrefix)))
            {
                // we are already instrumenting this,
                // don't instrument nested methods that belong to the same stacktrace
                // e.g. ExecuteReader() -> ExecuteReader(commandBehavior)
                return false;
            }

            return true;

            static bool HasDbType(Span span, string dbType)
            {
                if (span.Tags is SqlTags sqlTags)
                {
                    return sqlTags.DbType == dbType;
                }

                return span.GetTag(Tags.DbType) == dbType;
            }
        }

        /// <summary>
        /// Returns the modified command text, with extra data injected for DBM if necessary.
        /// </summary>
        private static string InjectDbmInfo(Tracer tracer, string commandText, CommandType commandType, IntegrationId integrationId, DbCommandCache.TagsCacheItem tagsFromConnectionString, Scope scope, SqlTags tags)
        {
            if (tracer.Settings.DbmPropagationMode != DbmPropagationLevel.Disabled
             && commandType != CommandType.StoredProcedure)
            {
                var alreadyInjected = commandText.StartsWith(DatabaseMonitoringPropagator.DbmPrefix);
                if (alreadyInjected)
                {
                    // The command text is already injected, so they're probably caching the SqlCommand
                    // that's not a problem if they're using 'service' mode, but it _is_ a problem for 'full' mode
                    // There's not a lot we can do about it (we don't want to start parsing commandText), so just
                    // report it for now
                    if (!Volatile.Read(ref _dbCommandCachingLogged)
                     && tracer.Settings.DbmPropagationMode != DbmPropagationLevel.Service)
                    {
                        _dbCommandCachingLogged = true;
                        var spanContext = scope.Span.Context;
                        Log.Warning(
                            "The {CommandType} IDbCommand instance already contains DBM information. Caching of the command objects is not supported with full DBM mode. [s_id: {SpanId}, t_id: {TraceId}]",
                            commandType,
                            spanContext.RawSpanId,
                            spanContext.RawTraceId);
                    }
                }
                else
                {
                    var propagatedCommand = DatabaseMonitoringPropagator.PropagateSpanData(tracer.Settings.DbmPropagationMode, tracer.DefaultServiceName, tagsFromConnectionString.DbName, tagsFromConnectionString.OutHost, scope.Span, integrationId, out var traceParentInjected);
                    if (!string.IsNullOrEmpty(propagatedCommand))
                    {
                        commandText = $"{propagatedCommand} {commandText}";
                        if (traceParentInjected)
                        {
                            tags.DbmTraceInjected = "true";
                        }
                    }
                }
            }

            return commandText;
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
                    string commandTypeName = commandTypeFullName.Substring(commandTypeFullName.LastIndexOf(".") + 1);
                    if (commandTypeName == "InterceptableDbCommand" || commandTypeName == "ProfiledDbCommand")
                    {
                        integrationId = null;
                        dbType = null;
                        return false;
                    }

                    const string commandSuffix = "Command";
                    int lastIndex = commandTypeFullName.LastIndexOf(".");
                    string namespaceName = lastIndex > 0 ? commandTypeFullName.Substring(0, lastIndex) : string.Empty;
                    integrationId = IntegrationId.AdoNet;
                    dbType = commandTypeName switch
                    {
                        _ when namespaceName.Length == 0 && commandTypeName == commandSuffix => "command",
                        _ when namespaceName.Contains(".") && commandTypeName == commandSuffix =>
                            // the + 1 could be dangerous and cause IndexOutOfRangeException, but this shouldn't happen
                            // a period should never be the last character in a namespace
                            namespaceName.Substring(namespaceName.LastIndexOf('.') + 1).ToLowerInvariant(),
                        _ when commandTypeName == commandSuffix =>
                            namespaceName.ToLowerInvariant(),
                        _ when commandTypeName.EndsWith(commandSuffix) =>
                            commandTypeName.Substring(0, commandTypeName.Length - commandSuffix.Length).ToLowerInvariant(),
                        _ => commandTypeName.ToLowerInvariant()
                    };
                    return true;
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

            // ServiceName cache
            private static KeyValuePair<string, string> _serviceNameCache;

            // ConnectionString tags cache
            private static KeyValuePair<string, DbCommandCache.TagsCacheItem> _tagsByConnectionStringCache;
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
                    var tagsFromConnectionString = GetTagsFromConnectionString(command);
                    return DbScopeFactory.CreateDbCommandScope(
                        tracer: tracer,
                        command: command,
                        integrationId: IntegrationId,
                        dbType: DbTypeName,
                        operationName: OperationName,
                        serviceName: GetServiceName(tracer, DbTypeName),
                        tagsFromConnectionString: ref tagsFromConnectionString);
                }

                // if command.GetType() != typeof(TCommand), we are probably instrumenting a method
                // defined in a base class like DbCommand and we can't use the cached values
                if (TryGetIntegrationDetails(commandType.FullName, out var integrationId, out var dbTypeName))
                {
                    var operationName = $"{dbTypeName}.query";
                    var tagsFromConnectionString = GetTagsFromConnectionString(command);
                    return DbScopeFactory.CreateDbCommandScope(
                        tracer: tracer,
                        command: command,
                        integrationId: integrationId.Value,
                        dbType: dbTypeName,
                        operationName: operationName,
                        serviceName: GetServiceName(tracer, dbTypeName),
                        tagsFromConnectionString: ref tagsFromConnectionString);
                }

                return null;
            }

#if NET6_0_OR_GREATER
            public static Scope CreateDbBatchScope(Tracer tracer, DbBatch batch)
            {
                var commandType = batch.GetType();

                if (commandType == CommandType)
                {
                    // use the cached values if command.GetType() == typeof(TCommand)
                    var tagsFromConnectionString = GetTagsFromConnectionString(batch);
                    return DbScopeFactory.CreateDbBatchScope(
                        tracer,
                        batch,
                        IntegrationId,
                        DbTypeName,
                        OperationName,
                        GetServiceName(tracer, DbTypeName),
                        ref tagsFromConnectionString);
                }

                // if command.GetType() != typeof(TCommand), we are probably instrumenting a method
                // defined in a base class and we can't use the cached values
                // TODO: since DbBatch is _already_ a base class and not an interface, do we really need this ?
                if (TryGetIntegrationDetails(commandType.FullName, out var integrationId, out var dbTypeName))
                {
                    var operationName = $"{dbTypeName}.query";
                    var tagsFromConnectionString = GetTagsFromConnectionString(batch);
                    return DbScopeFactory.CreateDbBatchScope(
                        tracer,
                        batch,
                        integrationId.Value,
                        dbTypeName,
                        operationName,
                        GetServiceName(tracer, dbTypeName),
                        ref tagsFromConnectionString);
                }

                return null;
            }
#endif

            private static string GetServiceName(Tracer tracer, string dbTypeName)
            {
                if (!tracer.CurrentTraceSettings.ServiceNames.TryGetValue(dbTypeName, out string serviceName))
                {
                    if (DbTypeName != dbTypeName)
                    {
                        // We cannot cache in the base class
                        return tracer.CurrentTraceSettings.GetServiceName(tracer, dbTypeName);
                    }

                    var serviceNameCache = _serviceNameCache;

                    // If not a base class
                    if (serviceNameCache.Key == tracer.DefaultServiceName)
                    {
                        // Service has not changed
                        // Fastpath
                        return serviceNameCache.Value;
                    }

                    // We create or replace the cache with the new service name
                    // Slowpath
                    var defaultServiceName = tracer.DefaultServiceName;
                    serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, dbTypeName);
                    _serviceNameCache = new KeyValuePair<string, string>(defaultServiceName, serviceName);
                }

                return serviceName;
            }

            private static DbCommandCache.TagsCacheItem GetTagsFromConnectionString(IDbCommand command)
            {
                string connectionString = null;
                try
                {
                    if (command.GetType().FullName == "System.Data.Common.DbDataSource.DbCommandWrapper")
                    {
                        return default;
                    }

                    connectionString = command.Connection?.ConnectionString;
                }
                catch (NotSupportedException nsException)
                {
                    Log.Debug(nsException, "ConnectionString cannot be retrieved from the command.");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error trying to retrieve the ConnectionString from the command.");
                }

                return GetTagsFromConnectionString(connectionString);
            }

#if NET6_0_OR_GREATER
            private static DbCommandCache.TagsCacheItem GetTagsFromConnectionString(DbBatch command)
            {
                string connectionString = null;
                try
                {
                    connectionString = command.Connection?.ConnectionString;
                }
                catch (NotSupportedException nsException)
                {
                    Log.Debug(nsException, "ConnectionString cannot be retrieved from the command.");
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "Error trying to retrieve the ConnectionString from the command.");
                }

                return GetTagsFromConnectionString(connectionString);
            }
#endif

            private static DbCommandCache.TagsCacheItem GetTagsFromConnectionString(string connectionString)
            {
                if (connectionString is null)
                {
                    return default;
                }

                // Check if the connection string is the one in the cache
                var tagsByConnectionString = _tagsByConnectionStringCache;
                if (tagsByConnectionString.Key == connectionString)
                {
                    // Fastpath
                    return tagsByConnectionString.Value;
                }

                // Cache the new tags by connection string
                // Slowpath
                var tags = DbCommandCache.GetTagsFromConnectionString(connectionString);
                _tagsByConnectionStringCache = new KeyValuePair<string, DbCommandCache.TagsCacheItem>(connectionString, tags);
                return tags;
            }
        }
    }
}
