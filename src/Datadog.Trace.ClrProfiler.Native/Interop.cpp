//---------------------------------------------------------------------------------------
// Exports that managed code from Datadog.Trace.ClrProfiler.Managed.dll will P/Invoke into
//
// NOTE: Must keep these signatures in sync with the DllImports in NativeMethods.cs!
//---------------------------------------------------------------------------------------

#include "CorProfiler.h"

EXTERN_C BOOL STDAPICALLTYPE GetMetadataNames(ModuleID moduleID,
                                              mdMethodDef methodDef,
                                              LPWSTR wszModulePath,
                                              ULONG cchModulePath,
                                              LPWSTR wszTypeDefName,
                                              ULONG cchTypeDefName,
                                              LPWSTR wszMethodDefName,
                                              ULONG cchMethodDefName)
{
    return g_pCallbackObject->GetMetadataNames(moduleID,
                                               methodDef,
                                               wszModulePath,
                                               cchModulePath,
                                               wszTypeDefName,
                                               cchTypeDefName,
                                               wszMethodDefName,
                                               cchMethodDefName);
}

EXTERN_C BOOL STDAPICALLTYPE IsProfilerAttached()
{
    return g_pCallbackObject->IsAttached();
}
