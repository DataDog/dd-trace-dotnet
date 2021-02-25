using System;

namespace Datadog.DynamicDiagnosticSourceBindings
{
    public interface IDynamicInvokerHandle
    {
        bool IsValid { get; }
    }
}
