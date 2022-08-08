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

EXTERN_C VOID STDAPICALLTYPE RemoveCallTargetDefinitions(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    return trace::profiler->InitializeProfiler(id, items, size, false);
}

EXTERN_C VOID STDAPICALLTYPE EnableByRefInstrumentation()
{
    return trace::profiler->EnableByRefInstrumentation();
}

EXTERN_C VOID STDAPICALLTYPE EnableCallTargetStateByRef()
{
    return trace::profiler->EnableCallTargetStateByRef();
}

EXTERN_C VOID STDAPICALLTYPE AddDerivedInstrumentations(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    return trace::profiler->AddDerivedInstrumentations(id, items, size);
}

EXTERN_C VOID STDAPICALLTYPE AddTraceAttributeInstrumentation(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                                              WCHAR* integration_type_name_ptr)
{
    return trace::profiler->AddTraceAttributeInstrumentation(id, integration_assembly_name_ptr,
                                                             integration_type_name_ptr);
}

EXTERN_C VOID STDAPICALLTYPE InitializeTraceMethods(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                                    WCHAR* integration_type_name_ptr, WCHAR* configuration_string_ptr)
{
    return trace::profiler->InitializeTraceMethods(id, integration_assembly_name_ptr, integration_type_name_ptr,
                                                   configuration_string_ptr);
}

EXTERN_C VOID STDAPICALLTYPE InstrumentProbes(
    debugger::DebuggerMethodProbeDefinition* methodProbes, 
    int methodProbesLength, 
    debugger::DebuggerLineProbeDefinition* lineProbes, 
    int lineProbesLength,
    debugger::DebuggerRemoveProbesDefinition* revertProbes,
    int revertProbesLength)
{
    return trace::profiler->InstrumentProbes(methodProbes, methodProbesLength, lineProbes, lineProbesLength, revertProbes, revertProbesLength);
}

EXTERN_C int STDAPICALLTYPE GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses)
{
    return trace::profiler->GetProbesStatuses(probeIds, probeIdsLength, probeStatuses);
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
