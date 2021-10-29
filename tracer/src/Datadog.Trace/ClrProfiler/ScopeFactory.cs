// <copyright file="ScopeFactory.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using System.Linq;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Convenience class that creates scopes and populates them with some standard details.
    /// </summary>
    internal static class ScopeFactory
    {
        public const string OperationName = "http.request";
        public const string ServiceName = "http-client";
        public const string DbIntegrationName = nameof(IntegrationIds.AdoNet);

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ScopeFactory));
        public static readonly IntegrationInfo DbIntegrationId = IntegrationRegistry.GetIntegrationInfo(DbIntegrationName);

        public static Scope GetActiveHttpScope(Tracer tracer)
        {
            var scope = tracer.ActiveScope;

            var parent = scope?.Span;

            if (parent != null &&
                parent.Type == SpanTypes.Http &&
                parent.GetTag(Tags.InstrumentationName) != null)
            {
                return scope;
            }

            return null;
        }

        /// <summary>
        /// Creates a span context for outbound http requests, or get the active one.
        /// Used to propagate headers without changing the active span.
        /// </summary>
        /// <param name="tracer">The tracer instance to use to create the span.</param>
        /// <param name="integrationId">The id of the integration creating this scope.</param>
        /// <returns>A span context to use to populate headers</returns>
        public static SpanContext CreateHttpSpanContext(Tracer tracer, IntegrationInfo integrationId)
        {
            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, skip this trace
                return null;
            }

            try
            {
                var activeScope = GetActiveHttpScope(tracer);

                if (activeScope != null)
                {
                    return activeScope.Span.Context;
                }

                return tracer.CreateSpanContext(serviceName: $"{tracer.DefaultServiceName}-{ServiceName}");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating span context.");
            }

            return null;
        }

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="tracer">The tracer instance to use to create the new scope.</param>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationId">The id of the integration creating this scope.</param>
        /// <param name="tags">The tags associated to the scope</param>
        /// <param name="spanId">The span ID</param>
        /// <returns>A new pre-populated scope.</returns>
        public static Scope CreateOutboundHttpScope(Tracer tracer, string httpMethod, Uri requestUri, IntegrationInfo integrationId, out HttpTags tags, ulong? spanId = null)
            => CreateOutboundHttpScope(tracer, httpMethod, requestUri, integrationId, out tags, spanId, startTime: null);

        /// <summary>
        /// Creates a scope for outbound http requests and populates some common details.
        /// </summary>
        /// <param name="tracer">The tracer instance to use to create the new scope.</param>
        /// <param name="httpMethod">The HTTP method used by the request.</param>
        /// <param name="requestUri">The URI requested by the request.</param>
        /// <param name="integrationId">The id of the integration creating this scope.</param>
        /// <param name="tags">The tags associated to the scope</param>
        /// <param name="spanId">The span ID</param>
        /// <param name="startTime">The start time that should be applied to the span</param>
        /// <returns>A new pre-populated scope.</returns>
        internal static Scope CreateOutboundHttpScope(Tracer tracer, string httpMethod, Uri requestUri, IntegrationInfo integrationId, out HttpTags tags, ulong? spanId, DateTimeOffset? startTime)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(integrationId) || PlatformHelpers.PlatformStrategy.ShouldSkipClientSpan(tracer.ActiveScope) || HttpBypassHelper.UriContainsAnyOf(requestUri, tracer.Settings.HttpClientExcludedUrlSubstrings))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                if (GetActiveHttpScope(tracer) != null)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. HttpClientHandler.SendAsync() -> SocketsHttpHandler.SendAsync()
                    return null;
                }

                string resourceUrl = requestUri != null ? UriHelpers.CleanUri(requestUri, removeScheme: true, tryRemoveIds: true) : null;
                string httpUrl = requestUri != null ? UriHelpers.CleanUri(requestUri, removeScheme: false, tryRemoveIds: false) : null;

                tags = new HttpTags();

                string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                scope = tracer.StartActiveWithTags(OperationName, tags: tags, serviceName: serviceName, spanId: spanId, startTime: startTime);

                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{httpMethod} {resourceUrl}";

                tags.HttpMethod = httpMethod?.ToUpperInvariant();
                tags.HttpUrl = httpUrl;
                tags.InstrumentationName = IntegrationRegistry.GetName(integrationId);

                tags.SetAnalyticsSampleRate(integrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }

        public static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            if (!tracer.Settings.IsIntegrationEnabled(DbIntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            var commandType = command.GetType();
            if (tracer.Settings.AdoNetExcludedTypes.Count > 0 && tracer.Settings.AdoNetExcludedTypes.Contains(commandType.FullName))
            {
                // AdoNet type disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                string dbType = GetDbType(commandType.Namespace, commandType.Name);

                if (dbType == null)
                {
                    // don't create a scope, skip this trace
                    return null;
                }

                Span parent = tracer.ActiveScope?.Span;

                if (parent != null &&
                    parent.Type == SpanTypes.Sql &&
                    parent.GetTag(Tags.DbType) == dbType &&
                    parent.ResourceName == command.CommandText)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. ExecuteReader() -> ExecuteReader(commandBehavior)
                    return null;
                }

                string serviceName = tracer.Settings.GetServiceName(tracer, dbType);
                string operationName = $"{dbType}.query";

                var tags = new SqlTags();
                scope = tracer.StartActiveWithTags(operationName, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                tags.DbType = dbType;

                span.AddTagsFromDbCommand(command);

                tags.SetAnalyticsSampleRate(DbIntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        public static string GetDbType(string namespaceName, string commandTypeName)
        {
            // First we try with the most commons ones. Avoiding the ComputeStringHash
            var result =
                commandTypeName switch
                {
                    "SqlCommand" => "sql-server",
                    "NpgsqlCommand" => "postgres",
                    "MySqlCommand" => "mysql",
                    "SqliteCommand" => "sqlite",
                    "SQLiteCommand" => "sqlite",
                    _ => null,
                };

            // If we add these cases to the previous switch the JIT will apply the ComputeStringHash codegen
            if (result != null ||
                commandTypeName == "InterceptableDbCommand" ||
                commandTypeName == "ProfiledDbCommand")
            {
                return result;
            }

            const string commandSuffix = "Command";

            // Now the uncommon cases
            return
                commandTypeName switch
                {
                    _ when namespaceName.Length == 0 && commandTypeName == commandSuffix => "command",
                    _ when namespaceName.Contains('.') && commandTypeName == commandSuffix =>
                        // the + 1 could be dangerous and cause IndexOutOfRangeException, but this shouldn't happen
                        // a period should never be the last character in a namespace
                        namespaceName.Substring(namespaceName.LastIndexOf('.') + 1).ToLowerInvariant(),
                    _ when commandTypeName == commandSuffix =>
                        namespaceName.ToLowerInvariant(),
                    _ when commandTypeName.EndsWith(commandSuffix) =>
                        commandTypeName.Substring(0, commandTypeName.Length - commandSuffix.Length).ToLowerInvariant(),
                    _ => commandTypeName.ToLowerInvariant()
                };
        }
    }
}
