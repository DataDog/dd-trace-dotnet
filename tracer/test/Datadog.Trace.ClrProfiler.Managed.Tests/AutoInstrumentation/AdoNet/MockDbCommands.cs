// <copyright file="MockDbCommands.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#pragma warning disable SA1403 // file may only contain a single namespace
#pragma warning disable SA1402 // file may only contain a single type
#pragma warning disable SA1502 // element should not be in a single line
#pragma warning disable SA1649 // file name should match first type name

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AdoNet;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.AdoNet
{
    public class MockDbCommand : DbCommand
    {
        private readonly MockDbParameterCollection _parameters = new MockDbParameterCollection();

        public override string CommandText { get; set; }

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        public override bool DesignTimeVisible { get; set; }

        protected override DbConnection DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => _parameters;

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

    public class MockDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        public override string ParameterName { get; set; }

        public override string SourceColumn { get; set; }

        public override DataRowVersion SourceVersion { get; set; }

        public override object Value { get; set; }

        public override byte Precision { get; set; }

        public override byte Scale { get; set; }

        public override int Size { get; set; }

        // TODO no clue what this is
        public override bool SourceColumnNullMapping { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void ResetDbType()
        {
            // No-op for mock
        }
    }

    public class MockDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _parameters = new List<DbParameter>();

        public override int Count => _parameters.Count;

        public override object SyncRoot => this;

        public override int Add(object value)
        {
            _parameters.Add((DbParameter)value);
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (DbParameter parameter in values)
            {
                _parameters.Add(parameter);
            }
        }

        public override void Clear() => _parameters.Clear();

        public override bool Contains(object value) => _parameters.Contains(value);

        public override bool Contains(string value) => _parameters.Any(p => p.ParameterName == value);

        public override void CopyTo(Array array, int index)
        {
            for (var i = 0; i < _parameters.Count; i++)
            {
                array.SetValue(_parameters[i], index + i);
            }
        }

        public override IEnumerator GetEnumerator() => _parameters.GetEnumerator();

        public override int IndexOf(object value) => _parameters.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName) => _parameters.FindIndex(p => p.ParameterName == parameterName);

        public override void Insert(int index, object value) => _parameters.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _parameters.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _parameters.RemoveAt(index);

        public override void RemoveAt(string parameterName) => _parameters.RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _parameters[index];

        protected override DbParameter GetParameter(string parameterName) => _parameters.Find(p => p.ParameterName == parameterName);

        protected override void SetParameter(int index, DbParameter value) => _parameters[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            int index = IndexOf(parameterName);
            if (index >= 0)
            {
                _parameters[index] = value;
            }
        }
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
