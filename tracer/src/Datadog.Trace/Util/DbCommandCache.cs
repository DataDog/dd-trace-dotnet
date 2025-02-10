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

        private static ConcurrentDictionary<string, TagsCacheItem>? _cache = new();

        /// <summary>
        /// Gets or sets the underlying cache, to be used for unit tests
        /// </summary>
        internal static ConcurrentDictionary<string, TagsCacheItem>? Cache
        {
            get
            {
                return _cache;
            }

            set
            {
                _cache = value;
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

            var cache = _cache;

            if (cache != null)
            {
                if (cache.TryGetValue(connectionString, out var tags))
                {
                    // Fast path: it's expected that most calls will end up in this branch
                    return tags;
                }

                if (cache.Count <= MaxConnectionStrings)
                {
                    // Populating the cache. This path should be hit only during application warmup
                    // ReSharper disable once ConvertClosureToMethodGroup -- Lambdas are cached by the compiler
                    return cache.GetOrAdd(connectionString, cs => ExtractTagsFromConnectionString(cs));
                }

                // The assumption "connection strings are a finite set" was wrong, disabling the cache
                // Use atomic operation to log only once
                if (Interlocked.Exchange(ref _cache, null) != null)
                {
                    Log.Information<int>("More than {MaxConnectionStrings} different connection strings were used, disabling cache", MaxConnectionStrings);
                }
            }

            // Fallback: too many different connection string, there might be a random part in them
            // Stop using the cache to prevent memory leaks
            return ExtractTagsFromConnectionString(connectionString);
        }

        private static TagsCacheItem ExtractTagsFromConnectionString(string connectionString)
        {
            try
            {
                // Parse the connection string
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

                // Extract the tags
                return new TagsCacheItem(
                    dbName: GetConnectionStringValue(builder, "Database", "Initial Catalog", "InitialCatalog"),
                    dbUser: GetConnectionStringValue(builder, "User ID", "UserID"),
                    outHost: GetConnectionStringValue(builder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host"));
            }
            catch (Exception)
            {
                // DbConnectionStringBuilder can throw exceptions if the connection string is invalid
                // in this case we should not use the connection string and just return default
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
