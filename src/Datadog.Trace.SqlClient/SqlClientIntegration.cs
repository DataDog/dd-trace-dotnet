namespace Datadog.Trace.SqlClient
{
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
        private static SqlClientDiagnosticListener _sqlListener;
        private static SqlClientEventListener _sqlClientEventListener;

        /// <summary>
        /// Enable the integration
        /// </summary>
        /// <param name="tracer">The tracer to use</param>
        /// <param name="serviceName">The service name that will be set on the spans created by the instrumentation</param>
        public static void Enable(Tracer tracer = null, string serviceName = null)
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
                    _sqlListener = new SqlClientDiagnosticListener(tracer ?? Tracer.Instance, serviceName);
                    _globalListener = new GlobalListener(SqlClientListenerName, _sqlListener);
                    _sqlClientEventListener = new SqlClientEventListener(tracer ?? Tracer.Instance, serviceName);
                }
            }
        }

        /// <summary>
        /// Disable the instrumentation
        /// </summary>
        public static void Disable()
        {
            lock (_lock)
            {
                if (!_isEnabled)
                {
                    return;
                }
                else
                {
                    _isEnabled = false;
                    _globalListener.Dispose();
                    _sqlListener = null;
                    _globalListener = null;
                    _sqlClientEventListener.Dispose();
                }
            }
        }
    }
}