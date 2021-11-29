//---------------------------------------------------------------------------------------
// Exports that managed code from Datadog.Trace.dll will
// P/Invoke into
//
// NOTE: Must keep these signatures in sync with the DllImports in
// NativeMethods.cs!
//---------------------------------------------------------------------------------------

#include "cor_profiler.h"

#ifndef _WIN32
#include <dlfcn.h>
#endif

EXTERN_C BOOL STDAPICALLTYPE IsProfilerAttached()
{
    return trace::profiler != nullptr && trace::profiler->IsAttached();
}

EXTERN_C VOID STDAPICALLTYPE GetAssemblyAndSymbolsBytes(BYTE** pAssemblyArray, int* assemblySize, BYTE** pSymbolsArray,
                                                        int* symbolsSize)
{
    return trace::profiler->GetAssemblyAndSymbolsBytes(pAssemblyArray, assemblySize, pSymbolsArray, symbolsSize);
}

EXTERN_C VOID STDAPICALLTYPE InitializeProfiler(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    return trace::profiler->InitializeProfiler(id, items, size);
}

EXTERN_C VOID STDAPICALLTYPE EnableByRefInstrumentation()
{
    return trace::profiler->EnableByRefInstrumentation();
}

#ifndef _WIN32
EXTERN_C void *dddlopen (const char *__file, int __mode)
{
    return dlopen(__file, __mode);
}

EXTERN_C char *dddlerror (void)
{
    return dlerror();
}

EXTERN_C void *dddlsym (void *__restrict __handle, const char *__restrict __name)
{
    return dlsym(__handle, __name);
}
#endif
