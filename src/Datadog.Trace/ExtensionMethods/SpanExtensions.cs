using System;
using System.Data;
using System.Data.Common;

namespace Datadog.Trace.ExtensionMethods
{
    /// <summary>
    /// Extension methods for the <see cref="Span"/> class.
    /// </summary>
    public static class SpanExtensions
    {
        /// <summary>
        /// Adds standard tags to a span with values taken from the specified <see cref="DbCommand"/>.
        /// </summary>
        /// <param name="span">The span to add the tags to.</param>
        /// <param name="command">The db command to get tags values from.</param>
        public static void AddTagsFromDbCommand(this Span span, IDbCommand command)
        {
            span.ResourceName = command.CommandText;
            span.Type = SpanTypes.Sql;

            // parse the connection string
            var builder = new DbConnectionStringBuilder { ConnectionString = command.Connection.ConnectionString };

            string database = GetConnectionStringValue(builder, "Database", "Initial Catalog", "InitialCatalog");
            span.SetTag(Tags.DbName, database);

            string user = GetConnectionStringValue(builder, "User ID", "UserID");
            span.SetTag(Tags.DbUser, user);

            string server = GetConnectionStringValue(builder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host");
            span.SetTag(Tags.OutHost, server);
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
