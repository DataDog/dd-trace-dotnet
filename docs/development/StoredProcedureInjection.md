# Stored Procedure DBM Propagation Injection

## Overview

When attempting to instrument SQL Server stored procedure executions with database monitoring (DBM) information, we faced a limitation that required us to modify the command from `CommandType.StoredProcedure` to `CommandType.Text`. While this change works for `Input` parameters, it fails for parameters with `ParameterDirection` values of `Output`, `InputOutput`, or `ReturnValue`. This document explains the technical reasons for this limitation and why we implemented a restriction to only convert stored procedures that have exclusively `Input` parameters.

## Technical Background

### How ADO.NET Executes Commands

ADO.NET provides two primary methods for executing database commands:

1. **RPC (Remote Procedure Call)** - Used for stored procedures, this mechanism sends the procedure name and parameters directly to SQL Server as an RPC call.
2. **SQL Batch** - Used for SQL text, this sends the entire command text to be parsed and executed by SQL Server.

When a `SqlCommand` is executed with `CommandType.StoredProcedure`, the internal execution path differs significantly from when it's executed with `CommandType.Text`:

```csharp
// For StoredProcedure
BuildRPC(inSchema, _parameters, ref rpc);  // Builds the RPC call format

// For Text
BuildExecuteSql(cmdBehavior, null, _parameters, ref rpc);  // Builds SQL batch format
```

### Why Can't We Inject into the StoredProcedure CommandText?

A fundamental issue is that with `CommandType.StoredProcedure`, the `CommandText` property is used as the **procedure name** itself, not as a SQL command to be executed. Examining the source code in `SqlCommand.cs`, we can see:

```csharp
Debug.Assert(this.CommandType == System.Data.CommandType.StoredProcedure, "invalid use of sp_prepare for stored proc invocation!");
// ...
rpc.rpcName = this.CommandText; // just get the raw command text
```

If we were to inject DBM comments into the `CommandText` for a stored procedure, the resulting value wouldn't be a valid procedure name:

```csharp

Debug.Assert(this.CommandType == System.Data.CommandType.StoredProcedure, "invalid use of sp_prepare for stored proc invocation!");
// ...
rpc.rpcName = this.CommandText; // just get the raw command text
```

This would result in SQL Server errors because it would try to find a procedure literally named `/*dddbs='service'*/ dbo.GetData` which doesn't exist.

### The Parameter Direction Challenge

The key issue is how parameters with different directions are handled:

#### For Stored Procedures (RPC):

- The ADO.NET driver sets up the parameter directions properly in the TDS protocol
- SQL Server explicitly returns values for `Output`, `InputOutput`, and `ReturnValue` parameters
- The driver automatically updates the `.Value` property of these parameters with the returned values

From `SqlCommand.cs`:

```csharp
// set output bit
if (parameter.Direction == ParameterDirection.InputOutput ||
    parameter.Direction == ParameterDirection.Output)
    rpc.paramoptions[j] |= TdsEnums.RPC_PARAM_BYREF;
```
#### For Text Commands:

- The parameters are sent as part of the SQL batch via parameter substitution
- There's no built-in mechanism to capture output values back into the original parameters
- The `.Value` property of parameters doesn't automatically update

## The Conversion Process

When instrumenting database calls for DBM, we must add tracking comments to the command:

```csharp
// Original stored procedure call
command.CommandType = CommandType.StoredProcedure;
command.CommandText = "dbo.sp_GetTableRow";

// Converted to text command with DBM comment
command.CommandType = CommandType.Text;
command.CommandText = "EXEC [dbo].[sp_GetTableRow] @Param1=@Param1 /*dddbs='service'...*/";
```

This conversion works perfectly for `Input` parameters, as we're simply passing the same values in a different format.

## The Core Issue

**When converting from `StoredProcedure` to `Text`, the automatic parameter value updating is lost.**

For a standard stored procedure call, SQL Server returns output parameter values, and ADO.NET's internal mechanisms update the parameter objects automatically. When we convert to a text command, our `EXEC` statement executes the procedure correctly, but there's no mechanism to capture these output values back to the original parameters.

This occurs because:

1. The TDS protocol handling differs between RPC and SQL batch execution
2. When using `CommandType.Text`, parameter values are substituted into the SQL string
3. The connection between the parameter objects and their output values is lost in the conversion

### Example of the Problem

```csharp
// Original code
command.CommandType = CommandType.StoredProcedure;
command.CommandText = "dbo.UpdateRecord";
var outParam = command.Parameters.AddWithValue("@OldValue", SqlDbType.VarChar, 100);
outParam.Direction = ParameterDirection.Output;
command.ExecuteNonQuery();
string oldValue = outParam.Value.ToString(); // This works!

// After our conversion
command.CommandType = CommandType.Text;
command.CommandText = "EXEC [dbo].[UpdateRecord] @OldValue=@OldValue OUTPUT /*dddbs='service'...*/";
command.ExecuteNonQuery();
string oldValue = outParam.Value.ToString(); // This returns the original value, not the output!
```

## Implementation Decision

Given this limitation, we implemented a safety check:

```csharp
// Check to see if we have any Return/InputOutput/Output parameters
if (command.Parameters != null) {
    foreach (DbParameter? param in command.Parameters) {
        if (param == null) {
            continue;
        }
        
        if (param.Direction != ParameterDirection.Input) {
            return false; // Do not attempt the conversion
        }
    }
}
```

This prevents us from silently breaking applications that depend on output parameters. Instead, we leave such commands unmodified, sacrificing DBM tracing for those specific calls to ensure application correctness.

## Conclusion

The limitation in converting stored procedure calls with non-input parameters to text commands is a fundamental issue with how ADO.NET and the TDS protocol handle different command types. While it would be technically possible to implement a custom solution that executes the procedure and then separately retrieves output values, such an approach would:

1. Add significant complexity
2. Require multiple round-trips to the database
3. Potentially introduce transaction and isolation level issues

Given these challenges, the current approach of only instrumenting stored procedures with exclusively input parameters represents the best balance between providing monitoring capabilities and ensuring application reliability.
