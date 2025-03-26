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
            await TestBasicStoredProc(connection, tableName, token); // just a basic query
            await TestOutputParameters(connection, tableName, token); // test OUTPUT and RETURN
            await TestTransactionScope(connection, tableName, token); // we had exceptions in transaction scopes before so ensure we handle these

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
                command.CommandTimeout = 5; // running into issues where something is hanging
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
                command.CommandTimeout = 5; // running into issues where something is hanging

                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "dbo.sp_UpdateRowWithOutput";

                // Input parameters - should be added to EXEC command
                command.Parameters.AddWithValue("@Id", 1);
                command.Parameters.AddWithValue("@NewName", "UpdatedName1");

                // Output parameter - shoudl be added to EXEC command
                var oldNameParam = command.Parameters.AddWithValue("@OldName", DBNull.Value);
                oldNameParam.Direction = ParameterDirection.Output;
                oldNameParam.Size = 100;

                // Return value parameter - should not be added to EXEC command
                var returnParam = command.Parameters.AddWithValue("@ReturnValue", DBNull.Value);
                returnParam.Direction = ParameterDirection.ReturnValue;

                await command.ExecuteNonQueryAsync(token);
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
            // a transaction si multiple commands that all must succeed or none at all
            try
            {
                using var transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);

                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 5; // running into issues where something is hanging

                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "dbo.sp_BatchUpdate";

                        command.Parameters.AddWithValue("@Id", DbType.Int32).Value = 2;
                        command.Parameters.AddWithValue("@NewName", DbType.String).Value = "UpdatedInTransaction1";

                        var returnParam = command.Parameters.AddWithValue("@ReturnValue", DBNull.Value);
                        returnParam.Direction = ParameterDirection.ReturnValue;

                        await command.ExecuteNonQueryAsync(token);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandTimeout = 5; // running into issues where something is hanging

                        command.CommandType = CommandType.StoredProcedure;
                        command.CommandText = "dbo.sp_BatchUpdate";

                        command.Parameters.AddWithValue("@Id", DbType.Int32).Value = 3;
                        command.Parameters.AddWithValue("@NewName", DbType.Int32).Value = "UpdatedInTransaction2";

                        var returnParam = command.Parameters.AddWithValue("@ReturnValue", DBNull.Value);
                        returnParam.Direction = ParameterDirection.ReturnValue;

                        await command.ExecuteNonQueryAsync(token);
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
