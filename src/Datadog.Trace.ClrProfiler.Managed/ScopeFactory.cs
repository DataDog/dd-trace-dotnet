using System;
using System.Data;
using Datadog.Trace.ClrProfiler.Integrations.AdoNet;
using Datadog.Trace.Configuration;
using Datadog.Trace.Conventions;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Convenience class that creates scopes and populates them with some standard details.
    /// </summary>
    internal static class ScopeFactory
    {
        public const string OperationName = "http.request";
        public const string ServiceName = "http-client";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ScopeFactory));

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
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(integrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            try
            {
                if (GetActiveHttpScope(tracer) != null)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. HttpClientHandler.SendAsync() -> SocketsHttpHandler.SendAsync()
                    return null;
                }

                var args = new OutboundHttpArgs(spanId, httpMethod, requestUri, integrationId);
                var scope = tracer.OutboundHttpConvention.CreateScope(args, out tags);
                return scope;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
                return null;
            }
        }

        public static Scope CreateDbCommandScope(Tracer tracer, IDbCommand command)
        {
            if (!tracer.Settings.IsIntegrationEnabled(AdoNetConstants.IntegrationId))
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
                string dbType = GetDbType(commandType.Name);

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

                tags.SetAnalyticsSampleRate(AdoNetConstants.IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        public static string GetDbType(string commandTypeName)
        {
            switch (commandTypeName)
            {
                case "SqlCommand":
                    return "sql-server";
                case "NpgsqlCommand":
                    return "postgres";
                case "MySqlCommand":
                    return "mysql";
                case "OracleCommand":
                    return "oracle";
                case "SqliteCommand":
                case "SQLiteCommand":
                    return "sqlite";
                case "InterceptableDbCommand":
                case "ProfiledDbCommand":
                    // don't create spans for these
                    return null;
                default:
                    const string commandSuffix = "Command";

                    // remove "Command" suffix if present
                    return commandTypeName.EndsWith(commandSuffix)
                               ? commandTypeName.Substring(0, commandTypeName.Length - commandSuffix.Length).ToLowerInvariant()
                               : commandTypeName.ToLowerInvariant();
            }
        }
    }
}
