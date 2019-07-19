using System;

namespace Datadog.Trace
{
    internal interface IActiveScopeAccess
    {
        long CreatedAtTicks { get; }

        Guid? ContextGuid { get; }

        int Priority { get; }

        Scope GetActiveScope(params object[] parameters);

        bool TrySetActiveScope(Scope scope, params object[] parameters);
    }
}
