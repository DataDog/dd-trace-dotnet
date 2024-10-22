// <copyright file="DbScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.DatabaseMonitoring;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbScopeFactory
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbScopeFactory));
        private static bool _dbCommandCachingLogged = false;

        private static Scope? CreateDbCommandScope(Tracer tracer, IDbCommand command, IntegrationId integrationId, string dbType, string operationName, string serviceName, ref DbCommandCache.TagsCacheItem tagsFromConnectionString)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId) || !tracer.Settings.IsIntegrationEnabled(IntegrationId.AdoNet))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope? scope = null;
            SqlTags tags;
            var commandText = command.CommandText ?? string.Empty;

            try
            {
                var parent = tracer.InternalActiveScope?.Span;

                if (parent is { Type: SpanTypes.Sql } &&
                    HasDbType(parent, dbType) &&
                    (parent.ResourceName == commandText || commandText.StartsWith(DatabaseMonitoringPropagator.DbmPrefix) || commandText == DatabaseMonitoringPropagator.SetContextCommand))
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. ExecuteReader() -> ExecuteReader(commandBehavior)
                    return null;
                }

                // We might block the SQL call from RASP depending on the query
                VulnerabilitiesModule.OnSqlQuery(commandText, integrationId);

                tags = tracer.CurrentTraceSettings.Schema.Database.CreateSqlTags();
                tags.DbType = dbType;
                tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);
                tags.DbName = tagsFromConnectionString.DbName;
                tags.DbUser = tagsFromConnectionString.DbUser;
                tags.OutHost = tagsFromConnectionString.OutHost;

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

                scope = tracer.StartActiveInternal(operationName, tags: tags, serviceName: serviceName);
                scope.Span.ResourceName = commandText;
                scope.Span.Type = SpanTypes.Sql;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(integrationId);
            }
            catch (Exception ex) when (ex is not BlockException)
            {
                Log.Error(ex, "Error creating or populating scope.");
                return scope;
            }

            try
            {
                if (tracer.Settings.DbmPropagationMode != DbmPropagationLevel.Disabled
                    && command.CommandType != CommandType.StoredProcedure)
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
                                command.CommandType,
                                spanContext.RawSpanId,
                                spanContext.RawTraceId);
                        }
                    }
                    else
                    {
                        var propagationComment = DatabaseMonitoringPropagator.PropagateDataViaComment(tracer.Settings.DbmPropagationMode, tracer.DefaultServiceName, tagsFromConnectionString.DbName, tagsFromConnectionString.OutHost, scope.Span, integrationId, out var traceParentInjectedInComment);
                        if (!string.IsNullOrEmpty(propagationComment))
                        {
                            command.CommandText = $"{propagationComment} {commandText}";
                        }

                        // try context injection only after comment injection, so that if it fails, we still have service level propagation
                        var traceParentInjectedInContext = DatabaseMonitoringPropagator.PropagateDataViaContext(tracer.Settings.DbmPropagationMode, integrationId, command.Connection, scope.Span);
                        if (traceParentInjectedInComment || traceParentInjectedInContext)
                        {
                            tags.DbmTraceInjected = "true";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error propagating span data for DBM");
            }

            // we have to start the span before doing the DBM propagation work (to have the span ID)
            // but ultimately, we don't want to measure the time spent instrumenting.
            scope.Span.ResetStartTime();

            return scope;

            static bool HasDbType(Span span, string dbType)
            {
                if (span.Tags is SqlTags sqlTags)
                {
                    return sqlTags.DbType == dbType;
                }

                return span.GetTag(Tags.DbType) == dbType;
            }
        }

        public static bool TryGetIntegrationDetails(
            string? commandTypeFullName,
            [NotNullWhen(true)] out IntegrationId? integrationId,
            [NotNullWhen(true)] out string? dbType)
        {
            // TODO: optimize this switch
            switch (commandTypeFullName)
            {
                case null:
                    integrationId = null;
                    dbType = null;
                    return false;
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
                    string commandTypeName = commandTypeFullName.Substring(commandTypeFullName.LastIndexOf(".", StringComparison.Ordinal) + 1);
                    if (IsDisabledCommandType(commandTypeName))
                    {
                        integrationId = null;
                        dbType = null;
                        return false;
                    }

                    const string commandSuffix = "Command";
                    int lastIndex = commandTypeFullName.LastIndexOf(".", StringComparison.Ordinal);
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

        internal static bool IsDisabledCommandType(string commandTypeName)
        {
            if (string.IsNullOrEmpty(commandTypeName))
            {
                return false;
            }

            var disabledTypes = Tracer.Instance.Settings.DisabledAdoNetCommandTypes;

            if (disabledTypes is null || disabledTypes.Count == 0)
            {
                return false;
            }

            foreach (var disabledType in disabledTypes)
            {
                if (string.Equals(disabledType, commandTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static class Cache<TCommand>
        {
            // ReSharper disable StaticMemberInGenericType
            // Static fields used intentionally to cache a different set of values for each TCommand.
            private static readonly Type CommandType;
            private static readonly string? DbTypeName;
            private static readonly string? OperationName;
            private static readonly IntegrationId IntegrationId;

            // ServiceName cache
            private static KeyValuePair<string, string> _serviceNameCache;

            // ConnectionString tags cache
            private static KeyValuePair<string, DbCommandCache.TagsCacheItem> _tagsByConnectionStringCache;
            // ReSharper restore StaticMemberInGenericType

            static Cache()
            {
                CommandType = typeof(TCommand);

                if (TryGetIntegrationDetails(CommandType.FullName, out var integrationId, out var dbTypeName))
                {
                    // cache values for this TCommand type
                    DbTypeName = dbTypeName;
                    OperationName = $"{DbTypeName}.query";
                    IntegrationId = integrationId.Value;
                }
            }

            public static Scope? CreateDbCommandScope(Tracer tracer, IDbCommand command)
            {
                var commandType = command.GetType();

                if (commandType == CommandType && DbTypeName is not null && OperationName is not null)
                {
                    // use the cached values if command.GetType() == typeof(TCommand)
                    // and we successfully called TryGetIntegrationDetails() in the ctor
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

            private static string GetServiceName(Tracer tracer, string dbTypeName)
            {
                if (!tracer.CurrentTraceSettings.ServiceNames.TryGetValue(dbTypeName, out var serviceName))
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
                string? connectionString = null;
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
                var tags = DbCommandCache.GetTagsFromDbCommand(command);
                _tagsByConnectionStringCache = new KeyValuePair<string, DbCommandCache.TagsCacheItem>(connectionString, tags);
                return tags;
            }
        }
    }
}
