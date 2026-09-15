// <copyright file="DbCommandCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using Datadog.Trace.Logging;
using Datadog.Trace.OpenTelemetry;

namespace Datadog.Trace.Util
{
    internal static class DbCommandCache
    {
        internal const int MaxConnectionStrings = 100;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbCommandCache));

        private static readonly SmallCacheOrNoCache<string, TagsCacheItem> _cache = new(MaxConnectionStrings, "connection strings");

        /// <summary>
        /// Gets the cache for unit tests
        /// </summary>
        internal static SmallCacheOrNoCache<string, TagsCacheItem> Cache
        {
            get
            {
                return _cache;
            }
        }

        public static TagsCacheItem GetTagsFromDbCommand(IDbCommand command, string? dbType)
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

            var tags = _cache.GetOrAdd(connectionString, dbType, static (cs, type) => ExtractTagsFromConnectionString(cs, type));

            // The cache is keyed by connection string alone, but the OpenTelemetry attributes also
            // depend on the provider. The same connection string being used by two providers is
            // pathological, so recalculate rather than grow the key.
            return tags.DbType == dbType ? tags : ExtractTagsFromConnectionString(connectionString, dbType);
        }

        private static TagsCacheItem ExtractTagsFromConnectionString(string connectionString, string? dbType)
        {
            try
            {
                // Parse the connection string
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                // Extract the tags
                var dbName = GetConnectionStringValue(builder, "Database", "Initial Catalog", "InitialCatalog");
                var outHost = GetConnectionStringValue(builder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host", "Hostname", "Host Name");

                // The OpenTelemetry attributes are shaped differently from the Datadog ones (the
                // host, the port, and a SQL Server instance name all share the "Server" keyword),
                // so calculate them here too, while we are already paying for the parse.
                DbSemanticConventions.GetConnectionAttributes(
                    dbType,
                    outHost,
                    GetConnectionStringValue(builder, "Port"),
                    dbName,
                    out var serverAddress,
                    out var serverPort,
                    out var dbNamespace);

                return new TagsCacheItem(
                    dbName: dbName,
                    dbUser: GetConnectionStringValue(builder, "User ID", "UserID", "User", "Uid", "Username", "User Name"),
                    outHost: outHost,
                    dbType: dbType,
                    dbNamespace: dbNamespace,
                    serverAddress: serverAddress,
                    serverPort: serverPort);
            }
            catch (Exception)
            {
                // DbConnectionStringBuilder can throw exceptions if the connection string is invalid
                // in this case we should not use the connection string and just return no tags.
                // The provider is still recorded so the cached entry is not treated as a mismatch.
                return new TagsCacheItem(dbName: null, dbUser: null, outHost: null, dbType: dbType, dbNamespace: null, serverAddress: null, serverPort: null);
            }
        }

        private static string? GetConnectionStringValue(DbConnectionStringBuilder builder, params string[] names)
        {
            foreach (string name in names)
            {
                // case-insensitive to the name/keyword
                if (builder.TryGetValue(name, out var valueObj) &&
                    valueObj is string value)
                {
                    return value;
                }
            }

            return null;
        }

        internal readonly struct TagsCacheItem
        {
            public readonly string? DbName;
            public readonly string? DbUser;
            public readonly string? OutHost;

            /// <summary>
            /// The Datadog "db.type" the OpenTelemetry attributes below were calculated for.
            /// </summary>
            public readonly string? DbType;

            /// <summary>
            /// The value to report in "db.namespace" when OpenTelemetry semantics are enabled.
            /// </summary>
            public readonly string? DbNamespace;

            /// <summary>
            /// The value to report in "server.address" when OpenTelemetry semantics are enabled.
            /// </summary>
            public readonly string? ServerAddress;

            /// <summary>
            /// The value to report in "server.port" when OpenTelemetry semantics are enabled, or
            /// <c>null</c> when the DBMS default port is in use.
            /// </summary>
            public readonly int? ServerPort;

            public TagsCacheItem(string? dbName, string? dbUser, string? outHost)
                : this(dbName, dbUser, outHost, dbType: null, dbNamespace: null, serverAddress: null, serverPort: null)
            {
            }

            public TagsCacheItem(string? dbName, string? dbUser, string? outHost, string? dbType, string? dbNamespace, string? serverAddress, int? serverPort)
            {
                DbName = dbName;
                DbUser = dbUser;
                OutHost = outHost;
                DbType = dbType;
                DbNamespace = dbNamespace;
                ServerAddress = serverAddress;
                ServerPort = serverPort;
            }
        }
    }
}
