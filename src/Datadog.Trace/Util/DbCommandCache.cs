using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Datadog.Trace.Util
{
    internal static class DbCommandCache
    {
        private static readonly ConcurrentDictionary<string, KeyValuePair<string, string>[]> Cache
            = new ConcurrentDictionary<string, KeyValuePair<string, string>[]>();

        public static KeyValuePair<string, string>[] GetTagsFromDbCommand(IDbCommand command)
        {
            return Cache.GetOrAdd(command.Connection.ConnectionString, connectionString =>
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
            });
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
