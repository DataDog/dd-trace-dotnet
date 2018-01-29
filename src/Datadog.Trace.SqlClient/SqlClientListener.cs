using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Datadog.Trace;
using Microsoft.Extensions.DiagnosticAdapter;

internal class SqlClientListener
{
    private readonly Tracer _tracer;
    private readonly string _serviceName;

    public SqlClientListener(Tracer tracer, string serviceName)
    {
        _tracer = tracer;
        _serviceName = serviceName;
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandBefore")]
    public void OnWriteCommandBefore(SqlCommand command)
    {
        var scope = _tracer.StartActive("sqlclient.command", serviceName: _serviceName);
        var span = scope.Span;
        span.ResourceName = command?.CommandText;
        span.SetTag(Tags.SqlQuery, command?.CommandText);
        span.Type = "sql";
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandAfter")]
    public void OnWriteCommandAfter(SqlCommand command, IDictionary<object, object> statistics)
    {
        _tracer.ActiveScope.Close();
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandError")]
    public void OnWriteCommandError(SqlCommand command, Exception exception)
    {
        using (var scope = _tracer.ActiveScope)
        {
            scope.Span.SetException(exception);
        }
    }
}