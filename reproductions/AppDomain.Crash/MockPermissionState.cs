using System;

namespace AppDomain.Crash
{
    [Serializable]
    public enum MockPermissionState
    {
        NoPermissions = 0,
        AppBaseAssembliesOnly = 1,
        DomainNeutralAssembliesAllowed = 2
    }
}