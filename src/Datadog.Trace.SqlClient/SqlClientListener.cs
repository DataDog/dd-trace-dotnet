using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Datadog.Trace;
using Microsoft.Extensions.DiagnosticAdapter;

internal class SqlClientListener
{
    private readonly Tracer _tracer;

    public SqlClientListener(Tracer tracer)
    {
        _tracer = tracer;
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandBefore")]
    public void OnWriteCommandBefore(Guid operationId, string operation, Guid connectionId, SqlCommand command)
    {
        Console.WriteLine($"OperationId:{operationId}");
        Console.WriteLine($"Operation:{operation}");
        Console.WriteLine($"ConnectionId:{connectionId}");
        Console.WriteLine($"Command:{command.CommandText}");
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandAfter")]
    public void OnWriteCommandAfter(Guid operationId, string operation, Guid connectionId, SqlCommand command, IDictionary<object, object> statistics, long timestamp)
    {
        Console.WriteLine($"OperationId:{operationId}");
        Console.WriteLine($"Operation:{operation}");
        Console.WriteLine($"ConnectionId:{connectionId}");
        Console.WriteLine($"Command:{command.CommandText}");
        Console.WriteLine($"Statistics:{string.Join(" ", statistics.Keys)}");
        Console.WriteLine($"Timestamp:{timestamp}");
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandError")]
    public void OnWriteCommandError(Guid operationId, string operation, Guid connectionId, SqlCommand command, Exception exception, long timestamp)
    {
        Console.WriteLine($"OperationId:{operationId}");
        Console.WriteLine($"Operation:{operation}");
        Console.WriteLine($"ConnectionId:{connectionId}");
        Console.WriteLine($"Command:{command.CommandText}");
        Console.WriteLine($"Exception:{exception.Message}");
        Console.WriteLine($"Timstamp:{timestamp}");
    }
}