// <copyright file="DbCommandCache.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

        private static ConcurrentDictionary<string, KeyValuePair<string, string>[]> _cache
            = new ConcurrentDictionary<string, KeyValuePair<string, string>[]>();

        /// <summary>
        /// Gets or sets the underlying cache, to be used for unit tests
        /// </summary>
        internal static ConcurrentDictionary<string, KeyValuePair<string, string>[]> Cache
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

        public static KeyValuePair<string, string>[] GetTagsFromDbCommand(IDbCommand command)
        {
            var connectionString = command.Connection.ConnectionString;
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
                    Log.Information($"More than {MaxConnectionStrings} different connection strings were used, disabling cache");
                }
            }

            // Fallback: too many different connection string, there might be a random part in them
            // Stop using the cache to prevent memory leaks
            return ExtractTagsFromConnectionString(connectionString);
        }

        private static KeyValuePair<string, string>[] ExtractTagsFromConnectionString(string connectionString)
        {
            // Parse the connection string
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            return new[]
            {
                new KeyValuePair<string, string>(
                    Tags.DbName,
                    GetConnectionStringValue(builder, "Database", "Initial Catalog", "InitialCatalog")),

                new KeyValuePair<string, string>(
                    Tags.DbUser,
                    GetConnectionStringValue(builder, "User ID", "UserID")),

                new KeyValuePair<string, string>(
                    Tags.OutHost,
                    GetConnectionStringValue(builder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host"))
            };
        }

        private static string GetConnectionStringValue(DbConnectionStringBuilder builder, params string[] names)
        {
            foreach (string name in names)
            {
                if (builder.TryGetValue(name, out object valueObj) &&
                    valueObj is string value)
                {
                    return value;
                }
            }

            return null;
        }
    }
}
