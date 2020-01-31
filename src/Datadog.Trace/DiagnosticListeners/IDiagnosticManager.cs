namespace Datadog.Trace.DiagnosticListeners
{
    internal interface IDiagnosticManager
    {
        void Start();

        void Stop();
    }
}
