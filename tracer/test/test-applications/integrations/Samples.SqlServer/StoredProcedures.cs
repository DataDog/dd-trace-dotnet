using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace Samples.SqlServer
{
    public class StoredProcedure
    {
        public static async Task RunStoredProcedureTestAsync(string connectionString, CancellationToken token)
        {
            using var connection = new SqlConnection(connectionString += ";Pooling=false");
            await connection.OpenAsync(token);
            Console.WriteLine("Starting SQL Server Stored Procedure Calls");
            // all the SQL here is based on DbCommandFactory

            var tableName = $"[Stored-Proc-System-Data-SqlClient-Test-{Guid.NewGuid():N}]";

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"CREATE TABLE {tableName} (Id int PRIMARY KEY, Name varchar(100));";
                await command.ExecuteNonQueryAsync(token);

                command.CommandText = $"INSERT INTO {tableName} (Id, Name) VALUES (1, 'Name1'), (2, 'Name2'), (3, 'Name3');";
                await command.ExecuteNonQueryAsync(token);
            }

            // Run various stored procedure tests
            try
            {
                await TestBasicStoredProc(connection, tableName, token); // just a basic query
                await TestOutputParameters(connection, tableName, token); // test OUTPUT and RETURN
                await TestTransactionScope(connection, tableName, token); // we had exceptions in transaction scopes before so ensure we handle these
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                // Clean up
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = $@"
IF OBJECT_ID('dbo.sp_GetTableRow', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_GetTableRow;
IF OBJECT_ID('dbo.sp_UpdateRowWithOutput', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_UpdateRowWithOutput;
IF OBJECT_ID('dbo.sp_BatchUpdate', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_BatchUpdate;
DROP TABLE {tableName};";
                    await command.ExecuteNonQueryAsync(token);
                }
            }

            connection.Close();
            SqlConnection.ClearAllPools(); // clear the connection pool to avoid issues with lingering connections
        }

        private static async Task TestBasicStoredProc(SqlConnection connection, string tableName, CancellationToken token)
        {
            // Similar to DbCommandFactory.GetSelectRowCommand
            Console.WriteLine("Test dbo.sp_GetTableRow");
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $@"
CREATE PROCEDURE dbo.sp_GetTableRow
    @Id int
AS
BEGIN
    SELECT * FROM {tableName} WHERE Id = @Id;
END";
                await command.ExecuteNonQueryAsync(token);
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = 5;
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "dbo.sp_GetTableRow";

                var param = command.CreateParameter();
                param.ParameterName = "@Id";
                param.Value = 2;
                command.Parameters.Add(param);

                using var reader = await command.ExecuteReaderAsync(token);
                while (await reader.ReadAsync(token))
                {
                    // consume the reader otherwise it locks
                }
            }
        }

        private static async Task TestOutputParameters(SqlConnection connection, string tableName, CancellationToken token)
        {
            Console.WriteLine("Test dbo.sp_UpdateRowWithOutput");

            // Similar to DbCommandFactory.GetUpdateRowCommand but with OUTPUT parameters
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $@"
CREATE PROCEDURE dbo.sp_UpdateRowWithOutput
    @Id int,
    @NewName varchar(100),
    @OldName varchar(100) OUTPUT
AS
BEGIN
    -- Store the old name before updating
    SELECT @OldName = Name FROM {tableName} WHERE Id = @Id;
    
    -- Update the row
    UPDATE {tableName} SET Name = @NewName WHERE Id = @Id;
    
    -- Return number of rows affected
    RETURN @@ROWCOUNT;
END";
                await command.ExecuteNonQueryAsync(token);
            }


            using (var command = connection.CreateCommand())
            {
                command.CommandTimeout = 5;

                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "dbo.sp_UpdateRowWithOutput";

                // Input parameters - should be added to EXEC command
                var param = command.CreateParameter();
                param.ParameterName = "@Id";
                param.Value = 1;
                command.Parameters.Add(param);

                var param2 = command.CreateParameter();
                param2.ParameterName = "@NewName";
                param2.Value = "UpdatedName1";
                command.Parameters.Add(param2);

                var param3 = command.CreateParameter();
                param3.ParameterName = "@OldName";
                param3.Direction = ParameterDirection.Output; // OUTPUT parameter
                param3.Value = DBNull.Value; // Initialize to null
                param3.Size = 100; // Set size for the output parameter
                command.Parameters.Add(param3);

                var param4 = command.CreateParameter();
                param4.ParameterName = "@ReturnValue";
                param4.Direction = ParameterDirection.ReturnValue; // RETURN value parameter
                param4.Value = DBNull.Value;
                command.Parameters.Add(param4);

                var count = await command.ExecuteNonQueryAsync(token);

                if (param3.Value == null || param3.Value == DBNull.Value)
                {
                    throw new Exception("OUTPUT parameter @OldName did not receive a value from stored procedure");
                }

                var oldName = param3.Value.ToString();
                if (oldName != "Name1") // We expect the original name to be "Name1"
                {
                    throw new Exception($"OUTPUT parameter @OldName has unexpected value: {oldName}, expected: Name1");
                }

                if (param4.Value == null || param4.Value == DBNull.Value)
                {
                    throw new Exception("RETURN value parameter did not receive a value from stored procedure");
                }

                var rowsAffected = Convert.ToInt32(param4.Value);
                if (rowsAffected != 1) // We expect 1 row to be affected
                {
                    throw new Exception($"RETURN value has unexpected value: {rowsAffected}, expected: 1");
                }
            }
        }

        private static async Task TestTransactionScope(SqlConnection connection, string tableName, CancellationToken token)
        {
            Console.WriteLine("Test: dbo.sp_BatchUpdate");

            using (var command = connection.CreateCommand())
            {
                command.CommandText = $@"
CREATE PROCEDURE dbo.sp_BatchUpdate
    @Id int,
    @NewName varchar(100)
AS
BEGIN
    -- Update the specified row
    UPDATE {tableName} SET Name = @NewName WHERE Id = @Id;
    
    -- Return affected rows
    RETURN @@ROWCOUNT;
END";
                await command.ExecuteNonQueryAsync(token);
            }

            // Test with transaction transactionScope - we had issues where the set context_info wasn't being
            // added to the transaction transactionScope and causing issues, so make sure we test it here too
            // a transaction is multiple commands that all must succeed or none at all
            try
            {
                using var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
                int returnValue1 = 0;
                int returnValue2 = 0;
                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 5;

                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "dbo.sp_BatchUpdate";

                        command.Parameters.AddWithValue("@Id", DbType.Int32).Value = 2;
                        command.Parameters.AddWithValue("@NewName", DbType.String).Value = "UpdatedInTransaction1";

                        var returnParam = command.Parameters.AddWithValue("@ReturnValue", DBNull.Value);
                        returnParam.Direction = ParameterDirection.ReturnValue;

                        await command.ExecuteNonQueryAsync(token);

                        returnValue1 = Convert.ToInt32(returnParam.Value);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 5;

                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "dbo.sp_BatchUpdate";

                        command.Parameters.AddWithValue("@Id", DbType.Int32).Value = 3;
                        command.Parameters.AddWithValue("@NewName", DbType.Int32).Value = "UpdatedInTransaction2";

                        var returnParam = command.Parameters.AddWithValue("@ReturnValue", DBNull.Value);
                        returnParam.Direction = ParameterDirection.ReturnValue;

                        await command.ExecuteNonQueryAsync(token);

                        returnValue2 = Convert.ToInt32(returnParam.Value);
                    }

                    if (returnValue1 != 1 || returnValue2 != 1)
                    {
                        throw new Exception($"Transaction stored procedures returned unexpected values: {returnValue1}, {returnValue2}. Expected both to be 1.");
                    }

                    transactionScope.Complete();
                    transactionScope.Dispose(); // Dispose the transactionScope to ensure it is completed
                }
                catch
                {

                    throw;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Transaction error: {ex.Message}");
                throw;
            }

        }
    }
}
