using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using Datadog.Trace;
using Microsoft.Extensions.DiagnosticAdapter;

internal class SqlClientListener
{
    private readonly Tracer _tracer;
    private readonly string _serviceName;

    // The ConditionalWeakTable makes sure we don't leak memory since it will not prevent objects it stores to be garbage collected
    private readonly ConditionalWeakTable<SqlCommand, Span> _currentSpans = new ConditionalWeakTable<SqlCommand, Span>();

    public SqlClientListener(Tracer tracer, string serviceName)
    {
        _tracer = tracer;
        _serviceName = serviceName;
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandBefore")]
    public void OnWriteCommandBefore(SqlCommand command)
    {
        var span = _tracer.StartSpan("sqlclient.command", serviceName: _serviceName);
        _currentSpans.Add(command, span);
        span.ResourceName = command?.CommandText;
        span.SetTag(Tags.SqlQuery, command?.CommandText);
        span.Type = "sql";
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandAfter")]
    public void OnWriteCommandAfter(SqlCommand command, IDictionary<object, object> statistics)
    {
        _currentSpans.TryGetValue(command, out Span span);
        if (span == null)
        {
            // TODO log
            return;
        }

       span.Finish();
    }

    [DiagnosticName("System.Data.SqlClient.WriteCommandError")]
    public void OnWriteCommandError(SqlCommand command, Exception exception)
    {
        _currentSpans.TryGetValue(command, out Span span);
        if (span == null)
        {
            // TODO log
            return;
        }

        span.SetException(exception);
        span.Finish();
   }
}