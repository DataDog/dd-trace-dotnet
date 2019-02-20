using System;
using System.Data.Common;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Interfaces;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;

namespace Datadog.Trace.ClrProfiler.Services
{
    internal class DbConnectionStringDecorator : ISpanDecorator
    {
        private readonly Func<string> _connectionStringSource;

        internal DbConnectionStringDecorator(Func<string> connectionStringSource)
        {
            _connectionStringSource = connectionStringSource;
        }

        public void Decorate(ISpan span)
        {
            var connectionString = _connectionStringSource();

            if (string.IsNullOrEmpty(connectionString))
            {
                return;
            }

            var connectionStringBuilder = new DbConnectionStringBuilder
                                          {
                                              ConnectionString = connectionString
                                          };

            var db = GetConnectionStringValue(connectionStringBuilder, "Database", "Initial Catalog", "InitialCatalog");
            var user = GetConnectionStringValue(connectionStringBuilder, "User ID", "UserID", "Uid", "User");
            var host = GetConnectionStringValue(connectionStringBuilder, "Server", "Data Source", "DataSource", "Network Address", "NetworkAddress", "Address", "Addr", "Host", "Hostname", "Dbq");

            span.Tag(Tags.DbName, db);
            span.Tag(Tags.DbUser, user);
            span.Tag(Tags.OutHost, host);
        }

        private static string GetConnectionStringValue(DbConnectionStringBuilder builder, params string[] keyNames)
        {
            foreach (var keyName in keyNames)
            {
                if (builder.TryGetValue(keyName, out var valueObj) &&
                    valueObj is string value)
                {
                    return value;
                }
            }

            return null;
        }
    }
}
