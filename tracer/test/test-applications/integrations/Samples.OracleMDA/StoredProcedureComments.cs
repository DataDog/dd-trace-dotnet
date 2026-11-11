// <copyright file="StoredProcedureComments.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace Samples.OracleMDA
{
    internal static class StoredProcedureComments
    {
        public static async Task RunAsync(OracleConnection connection)
        {
            var packageName = "DD_SP_COMMENT_" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpperInvariant();
            var schemaName = new OracleConnectionStringBuilder(connection.ConnectionString).UserID.ToUpperInvariant();
            var mode = Environment.GetEnvironmentVariable("DD_DBM_PROPAGATION_MODE");
#if NETFRAMEWORK
            // The tracer skips .NET Framework stored procedures: comments would break ODP.NET's implicit REF CURSOR configuration lookup.
            var expectComment = false;
#else
            var expectComment = string.Equals(mode, "service", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "dynamic_service", StringComparison.OrdinalIgnoreCase);
#endif
            var service = Environment.GetEnvironmentVariable("DD_SERVICE");
            if (string.IsNullOrEmpty(service))
            {
                throw new InvalidOperationException("Set DD_SERVICE for the stored-procedure comment checks.");
            }

            var packageCreated = false;
            try
            {
                using (var setup = connection.CreateCommand())
                {
                    setup.CommandText = $@"CREATE PACKAGE {packageName} AS
    PROCEDURE no_args;
    PROCEDURE with_args(p_in IN NUMBER, p_io IN OUT NUMBER, p_out OUT NUMBER);
    FUNCTION with_return(p_in IN NUMBER, p_io IN OUT NUMBER, p_out OUT NUMBER) RETURN NUMBER;
    PROCEDURE with_cursor(p_cursor OUT SYS_REFCURSOR);
END;";
                    await setup.ExecuteNonQueryAsync();
                    packageCreated = true;
                    setup.CommandText = $@"CREATE PACKAGE BODY {packageName} AS
    PROCEDURE no_args AS BEGIN NULL; END;
    PROCEDURE with_args(p_in IN NUMBER, p_io IN OUT NUMBER, p_out OUT NUMBER) AS
    BEGIN p_io := p_io + 2; p_out := p_in + 5; END;
    FUNCTION with_return(p_in IN NUMBER, p_io IN OUT NUMBER, p_out OUT NUMBER) RETURN NUMBER AS
    BEGIN p_io := p_io + 2; p_out := p_in + 5; RETURN p_in + 9; END;
    PROCEDURE with_cursor(p_cursor OUT SYS_REFCURSOR) AS
    BEGIN OPEN p_cursor FOR SELECT 1 FROM DUAL; END;
END;";
                    await setup.ExecuteNonQueryAsync();
                }

                foreach (var bindByName in new[] { false, true })
                {
                    foreach (var routine in new[] { "no_args", "with_args", "with_return", "with_cursor" })
                    {
                        // Cover quoted/schema-qualified package names without parsing or rewriting them in the tracer.
                        var procedureName = $"\"{schemaName}\".\"{packageName}\".\"{routine.ToUpperInvariant()}\"";
                        using (var command = connection.CreateCommand())
                        {
                            command.CommandText = procedureName;
                            command.CommandType = CommandType.StoredProcedure;
                            command.BindByName = bindByName;
                            var isFunction = routine == "with_return";
                            var hasArguments = routine == "with_args" || isFunction;
                            if (isFunction)
                            {
                                command.Parameters.Add("return_value", OracleDbType.Decimal).Direction = ParameterDirection.ReturnValue;
                            }

                            if (hasArguments)
                            {
                                command.Parameters.Add("p_in", OracleDbType.Decimal).Value = 11;
                                command.Parameters.Add("p_io", OracleDbType.Decimal).Direction = ParameterDirection.InputOutput;
                                command.Parameters.Add("p_out", OracleDbType.Decimal).Direction = ParameterDirection.Output;
                            }

                            if (routine == "with_cursor")
                            {
                                command.Parameters.Add("p_cursor", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                            }

                            command.Prepare();
                            string previousCommandText = null;
                            foreach (var asynchronous in new[] { false, true })
                            {
                                if (hasArguments)
                                {
                                    command.Parameters["p_io"].Value = 20;
                                }

                                using (var transaction = connection.BeginTransaction())
                                {
                                    command.Transaction = transaction;
                                    if (routine == "with_cursor")
                                    {
                                        using (var reader = asynchronous ? await command.ExecuteReaderAsync() : command.ExecuteReader())
                                        {
                                            Require(asynchronous ? await reader.ReadAsync() : reader.Read(), "Ref cursor did not return a row.");
                                            Require(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture) == 1, "Ref cursor result changed.");
                                        }
                                    }
                                    else if (asynchronous)
                                    {
                                        await command.ExecuteNonQueryAsync();
                                    }
                                    else
                                    {
                                        command.ExecuteNonQuery();
                                    }

                                    Require(command.Transaction == transaction, "Command transaction changed.");
                                    transaction.Rollback();
                                    command.Transaction = null;
                                }

                                if (hasArguments)
                                {
                                    Require(Number(command.Parameters["p_io"].Value) == 22 && Number(command.Parameters["p_out"].Value) == 16, "Output or input/output parameter changed.");
                                }

                                if (isFunction)
                                {
                                    Require(Number(command.Parameters["return_value"].Value) == 20, "Function return value changed.");
                                }

                                Require(command.CommandType == CommandType.StoredProcedure && command.BindByName == bindByName, "Stored-procedure command shape changed.");
                                if (expectComment)
                                {
                                    Require(command.CommandText.StartsWith("/*dddbs='", StringComparison.Ordinal), "Service comment missing from stored-procedure call.");
                                    Require(command.CommandText.Contains("ddps='" + Uri.EscapeDataString(service) + "'"), "Application service name missing from comment.");
                                    Require(command.CommandText.EndsWith(" " + procedureName, StringComparison.Ordinal), "Procedure name changed.");
                                    Require(!command.CommandText.Contains("traceparent="), "Service comment unexpectedly contains trace context.");
                                }
                                else
                                {
                                    Require(command.CommandText == procedureName, "Stored-procedure text changed outside service mode.");
                                }

                                Require(previousCommandText == null || command.CommandText == previousCommandText, "Reused command received another comment.");
                                previousCommandText = command.CommandText;
                                Console.WriteLine($"Stored procedure check passed: routine={routine}, bindByName={bindByName}, asynchronous={asynchronous}");
                            }
                        }
                    }
                }

                // Check Oracle's generated invocation, not just the client-side CommandText. No bind values are read or logged.
                using (var inspect = connection.CreateCommand())
                {
                    inspect.BindByName = true;
                    inspect.CommandText = "SELECT SQL_TEXT FROM SYS.V_$SQL WHERE COMMAND_TYPE = 47 AND INSTR(SQL_TEXT, :package_name) > 0";
                    inspect.Parameters.Add("package_name", OracleDbType.Varchar2).Value = packageName;
                    var invocationCount = 0;
                    using (var reader = await inspect.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            invocationCount++;
                            Require(reader.GetString(0).Contains("ddps='" + Uri.EscapeDataString(service) + "'") == expectComment, "Server invocation comment differs from configured mode.");
                        }
                    }

                    Require(invocationCount > 0, "Oracle generated invocation was not observed.");
                    Console.WriteLine("Stored procedure server comment checks passed.");
                }
            }
            finally
            {
                if (packageCreated)
                {
                    using (var cleanup = connection.CreateCommand())
                    {
                        cleanup.CommandText = $"DROP PACKAGE {packageName}";
                        await cleanup.ExecuteNonQueryAsync();
                    }
                }
            }

            Console.WriteLine("Stored procedure comment checks passed.");
        }

        private static decimal Number(object value) => value is OracleDecimal number ? number.Value : Convert.ToDecimal(value, CultureInfo.InvariantCulture);

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
