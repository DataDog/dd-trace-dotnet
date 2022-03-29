// <copyright file="DbType.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    /// <summary>
    /// Values for the "db.type" span tag.
    /// </summary>
    /// <seealso cref="Tags.DbType"/>
    internal static class DbType
    {
        public const string MySql = "mysql";

        public const string PostgreSql = "postgres";

        public const string Oracle = "oracle";

        public const string SqlServer = "sql-server";

        public const string Sqlite = "sqlite";
    }
}
