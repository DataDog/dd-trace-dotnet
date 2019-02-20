using System;
using System.Collections.Generic;
using System.Data;
using Datadog.Trace.Enums;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ExtensionMethods
{
    internal static class EnumExtensions
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(EnumExtensions));

        private static readonly Dictionary<DbProviderType, string> DbProviderTypeTagNameMap = new Dictionary<DbProviderType, string>
                                                                                              {
                                                                                                  { DbProviderType.SqlServer, "sql-server" },
                                                                                                  { DbProviderType.Postgress, "postgres" },
                                                                                                  { DbProviderType.MySql, "mysql" },
                                                                                                  { DbProviderType.Oracle, "oracle" },
                                                                                                  { DbProviderType.Db2, "db2" },
                                                                                                  { DbProviderType.Sqlite, "sqlite" }
                                                                                              };

        private static readonly Dictionary<string, DbProviderType> DbCommandTypeDbProviderTypeMap = new Dictionary<string, DbProviderType>(StringComparer.OrdinalIgnoreCase)
                                                                                                    {
                                                                                                        { "SqlCommand", DbProviderType.SqlServer },
                                                                                                        { "NpgsqlCommand", DbProviderType.Postgress },
                                                                                                        { "MySqlCommand", DbProviderType.MySql },
                                                                                                        { "OracleCommand", DbProviderType.Oracle },
                                                                                                        { "Db2Command", DbProviderType.Db2 },
                                                                                                        { "SQLiteCommand", DbProviderType.Sqlite }
                                                                                                    };

        internal static string ToTagName(this DbProviderType dbProviderType)
        {
            if (DbProviderTypeTagNameMap.TryGetValue(dbProviderType, out var dbTagName))
            {
                return dbTagName;
            }

            if (dbProviderType == DbProviderType.Unknown)
            {
                return null;
            }

            var exception = new ArgumentOutOfRangeException(nameof(dbProviderType));

            Log.DebugException($"DbProviderType value [{dbProviderType.ToString()}] not mapped to tag name", exception);

#if DEBUG
            throw exception;
#endif
        }

        internal static DbProviderType ToDbProviderType(this IDbCommand dbCommand)
        {
            if (DbCommandTypeDbProviderTypeMap.TryGetValue(dbCommand.GetType().Name, out var dbProviderType))
            {
                return dbProviderType;
            }

            Log.DebugFormat("IDbCommand type [{0}] not mapped to DbProviderType", dbCommand.GetType().Name);

            return DbProviderType.Unknown;
        }

        internal static string ToTagName(this IDbCommand dbCommand)
        {
            var knownDbProviderTypeTagName = ToDbProviderType(dbCommand).ToTagName();

            if (knownDbProviderTypeTagName != null)
            {
                return knownDbProviderTypeTagName;
            }

            const string iDbCommandSuffix = "Command";

            var commandTypeName = dbCommand.GetType().Name;

            // Remove "Command" suffix if present
            return commandTypeName.EndsWith(iDbCommandSuffix, StringComparison.OrdinalIgnoreCase)
                       ? commandTypeName.Substring(0, commandTypeName.Length - iDbCommandSuffix.Length)
                                        .ToLowerInvariant()
                       : commandTypeName.ToLowerInvariant();
        }
    }
}
