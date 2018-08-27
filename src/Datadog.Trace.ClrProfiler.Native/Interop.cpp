//---------------------------------------------------------------------------------------
// Exports that managed code from Datadog.Trace.ClrProfiler.Managed.dll will
// P/Invoke into
//
// NOTE: Must keep these signatures in sync with the DllImports in
// NativeMethods.cs!
//---------------------------------------------------------------------------------------

#include "CorProfiler.h"

EXTERN_C BOOL STDAPICALLTYPE IsProfilerAttached() {
  return g_pCallbackObject->IsAttached();
}
