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

        public static TagsCacheItem GetTagsFromDbCommand(IDbCommand command)
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

            // ReSharper disable once ConvertClosureToMethodGroup -- Lambdas are cached by the compiler
            return _cache.GetOrAdd(connectionString, cs => ExtractTagsFromConnectionString(cs));
        }

        private static TagsCacheItem ExtractTagsFromConnectionString(string connectionString)
        {
            try
            {
                // Parse the connection string
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                // Extract the tags
                var dbName = GetConnectionStringValue(builder, "Database", "Initial Catalog", "InitialCatalog");
                var dbUser = GetConnectionStringValue(builder, "User ID", "UserID");
                var outHost = GetConnectionStringValue(builder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host");

                Log.Information(
                    "DBM: Extracted connection string metadata. DbName: {DbNamePresent}, DbUser: {DbUserPresent}, OutHost: {OutHostPresent}",
                    !string.IsNullOrEmpty(dbName) ? "present" : "missing",
                    !string.IsNullOrEmpty(dbUser) ? "present" : "missing",
                    !string.IsNullOrEmpty(outHost) ? "present" : "missing");

                return new TagsCacheItem(dbName, dbUser, outHost);
            }
            catch (Exception ex)
            {
                // DbConnectionStringBuilder can throw exceptions if the connection string is invalid
                // in this case we should not use the connection string and just return default
                Log.Warning(ex, "DBM: Failed to parse connection string for metadata extraction");
                return default;
            }
        }

        private static string? GetConnectionStringValue(DbConnectionStringBuilder builder, params string[] names)
        {
            foreach (string name in names)
            {
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

            public TagsCacheItem(string? dbName, string? dbUser, string? outHost)
            {
                DbName = dbName;
                DbUser = dbUser;
                OutHost = outHost;
            }
        }
    }
}
