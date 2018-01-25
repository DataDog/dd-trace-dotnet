using Datadog.Trace;

/// <summary>
/// This integration instruments System.Data.SqlClient to provide detailed timing
/// information and available metadata on the SQL queries issued by your
/// application.
/// </summary>
public static class SqlClientIntegration
{
    private const string SqlClientListenerName = "SqlClientDiagnosticListener";
    private static object _lock = new object();
    private static bool _isEnabled;
    private static GlobalListener _globalListener;
    private static SqlClientListener _sqlListener;

    /// <summary>
    /// Enable the integration
    /// </summary>
    /// <param name="tracer">The tracer to use</param>
    public static void Enable(Tracer tracer = null)
    {
        lock (_lock)
        {
            if (_isEnabled)
            {
                return;
            }
            else
            {
                _isEnabled = true;
                _sqlListener = new SqlClientListener(tracer ?? Tracer.Instance);
                _globalListener = new GlobalListener(SqlClientListenerName, _sqlListener);
            }
        }
    }
}