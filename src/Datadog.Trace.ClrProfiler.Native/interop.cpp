//---------------------------------------------------------------------------------------
// Exports that managed code from Datadog.Trace.ClrProfiler.Managed.dll will
// P/Invoke into
//
// NOTE: Must keep these signatures in sync with the DllImports in
// NativeMethods.cs!
//---------------------------------------------------------------------------------------

#include <istream>
#include "cor_profiler.h"

EXTERN_C BOOL STDAPICALLTYPE IsProfilerAttached() {
  return trace::profiler->IsAttached();
}

EXTERN_C BOOL STDAPICALLTYPE AddIntegrations(const char* integrations_json) {
  std::stringstream ss;
  ss << integrations_json;
  return trace::profiler->AddIntegrations(ss);
}
