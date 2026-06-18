// <copyright file="DbOtelHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class DbOtelHelper
    {
        private static readonly Dictionary<string, string> DbSystemNameMap = new Dictionary<string, string>
        {
            [DbType.PostgreSql] = "postgresql",
            [DbType.SqlServer] = "microsoft.sql_server",
            [DbType.MySql] = "mysql",
            [DbType.Oracle] = "oracle.db",
            [DbType.Sqlite] = "sqlite",
        };

        internal static void SetDatabaseAttributes(
            ISpan span,
            string dbType,
            string? dbName,
            string? outHost,
            string? port,
            string commandText,
            bool peerServiceEnabled)
        {
            // db.system.name (always set)
            var systemName = DbSystemNameMap.TryGetValue(dbType, out var mapped) ? mapped : dbType;
            span.SetTag("db.system.name", systemName);

            // db.namespace
            if (!StringUtil.IsNullOrEmpty(dbName))
            {
                span.SetTag("db.namespace", dbName);
            }

            // server.address
            if (!StringUtil.IsNullOrEmpty(outHost))
            {
                span.SetTag("server.address", outHost);
            }

            // server.port
            if (!StringUtil.IsNullOrEmpty(port))
            {
                span.SetTag("server.port", port);
            }

            // db.query.text + db.operation.name + db.collection.name
            if (!StringUtil.IsNullOrEmpty(commandText))
            {
                span.SetTag("db.query.text", commandText);

                var (operation, table) = SqlQueryParser.Parse(commandText);
                if (!StringUtil.IsNullOrEmpty(operation))
                {
                    span.SetTag("db.operation.name", operation);
                }

                if (!StringUtil.IsNullOrEmpty(table))
                {
                    span.SetTag("db.collection.name", table);
                }
            }

            // peer.service (V1 schema compatibility)
            if (peerServiceEnabled)
            {
                var peerService = dbName ?? outHost;
                if (!StringUtil.IsNullOrEmpty(peerService))
                {
                    span.SetTag(Tags.PeerService, peerService);
                }
            }
        }
    }
}
