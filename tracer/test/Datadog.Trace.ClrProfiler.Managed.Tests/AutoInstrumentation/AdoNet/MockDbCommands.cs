// <copyright file="MockDbCommands.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1403 // file may only contain a single namespace
#pragma warning disable SA1402 // file may only contain a single type
#pragma warning disable SA1502 // element should not be in a single line
#pragma warning disable SA1649 // file name should match first type name

using System.Data;
using System.Data.Common;
using Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AdoNet;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AdoNet
{
    public class MockDbCommand : DbCommand
    {
        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override bool DesignTimeVisible { get; set; }

        protected override DbConnection DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => null;

        protected override DbTransaction DbTransaction { get; set; }

        public override void Prepare()
        {
        }

        public override void Cancel()
        {
        }

        public override int ExecuteNonQuery() => 0;

        public override object ExecuteScalar() => null;

        protected override DbParameter CreateDbParameter() => null;

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => null;
    }
}

#if !NETFRAMEWORK
// System.Data.SqlClient.SqlCommand this is always defined in .NET Framework
namespace System.Data.SqlClient { public class SqlCommand : MockDbCommand { } }
#endif

namespace Microsoft.Data.SqlClient { public class SqlCommand : MockDbCommand { } }

namespace MySql.Data.MySqlClient { public class MySqlCommand : MockDbCommand { } }

namespace MySqlConnector { public class MySqlCommand : MockDbCommand { } }

namespace Npgsql { public class NpgsqlCommand : MockDbCommand { } }

namespace Microsoft.Data.Sqlite { public class SqliteCommand : MockDbCommand { } }

// ReSharper disable once InconsistentNaming
namespace System.Data.SQLite { public class SQLiteCommand : MockDbCommand { } }

namespace Oracle.ManagedDataAccess.Client { public class OracleCommand : MockDbCommand { } }

namespace Oracle.DataAccess.Client { public class OracleCommand : MockDbCommand { } }

#pragma warning restore SA1649
#pragma warning restore SA1502
#pragma warning restore SA1402
#pragma warning restore SA1403
