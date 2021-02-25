using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    internal sealed class NoOpDisposable : IDisposable
    {
        public static readonly NoOpDisposable SingeltonInstance = new NoOpDisposable();

        public void Dispose()
        {
        }
    }
}
