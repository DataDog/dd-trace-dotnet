//---------------------------------------------------------------------------------------
// Exports that managed code from Datadog.Trace.ClrProfiler.Managed.dll will
// P/Invoke into
//
// NOTE: Must keep these signatures in sync with the DllImports in
// NativeMethods.cs!
//---------------------------------------------------------------------------------------

#include "cor_profiler.h"
#include "loader.h"

EXTERN_C BOOL STDAPICALLTYPE IsProfilerAttached() {
  return trace::profiler->IsAttached();
}

EXTERN_C BOOL STDAPICALLTYPE GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray, int* symbolsSize, AppDomainID appDomainId) {
    return trace::loader->GetAssemblyAndSymbolsBytes(pAssemblyArray, assemblySize, pSymbolsArray, symbolsSize, appDomainId);
}
