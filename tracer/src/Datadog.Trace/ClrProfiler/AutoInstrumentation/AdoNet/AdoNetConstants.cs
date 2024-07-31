// <copyright file="AdoNetConstants.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using static Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet.AdoNetClientInstrumentMethodsAttribute;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AdoNet
{
    internal static class AdoNetConstants
    {
        public static class TypeNames
        {
            public const string CommandBehavior = "System.Data.CommandBehavior";

            public const string DbDataReaderType = "System.Data.Common.DbDataReader";
            public const string DbDataReaderTaskType = "System.Threading.Tasks.Task`1[System.Data.Common.DbDataReader]";

            public const string Int32TaskType = "System.Threading.Tasks.Task`1[System.Int32]";
            public const string ObjectTaskType = "System.Threading.Tasks.Task`1[System.Object]";
        }

        public static class MethodNames
        {
            public const string ExecuteNonQuery = "ExecuteNonQuery";
            public const string ExecuteNonQueryAsync = "ExecuteNonQueryAsync";

            public const string ExecuteScalar = "ExecuteScalar";
            public const string ExecuteScalarAsync = "ExecuteScalarAsync";

            public const string ExecuteReader = "ExecuteReader";
            public const string ExecuteReaderAsync = "ExecuteReaderAsync";

            public const string ExecuteDbDataReader = "ExecuteDbDataReader";
            public const string ExecuteDbDataReaderAsync = "ExecuteDbDataReaderAsync";

            public const string Read = "Read";
            public const string ReadAsync = "ReadAsync";

            public const string Close = "Close";
            public const string CloseAsync = "CloseAsync";

            public const string GetString = "GetString";
            public const string GetValue = "GetValue";
        }
    }
}
