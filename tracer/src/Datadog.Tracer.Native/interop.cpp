//---------------------------------------------------------------------------------------
// Exports that managed code from Datadog.Trace.dll will
// P/Invoke into
//
// NOTE: Must keep these signatures in sync with the DllImports in
// NativeMethods.cs!
//---------------------------------------------------------------------------------------

#include "cor_profiler.h"
#include "logger.h"

#ifndef _WIN32
#include <dlfcn.h>
#undef EXTERN_C
#define EXTERN_C extern "C" __attribute__((visibility("default")))
#endif

EXTERN_C BOOL STDAPICALLTYPE IsProfilerAttached()
{
    return trace::profiler != nullptr && trace::profiler->IsAttached();
}

EXTERN_C VOID STDAPICALLTYPE GetAssemblyAndSymbolsBytes(void* typeHandle, BYTE** pAssemblyArray, BYTE** pSymbolsArray)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in GetAssemblyAndSymbolsBytes call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->GetAssemblyAndSymbolsBytes(typeHandle, pAssemblyArray, pSymbolsArray);
}

EXTERN_C VOID STDAPICALLTYPE InitializeProfiler(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in InitializeProfiler call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->InitializeProfiler(id, items, size);
}

EXTERN_C VOID STDAPICALLTYPE RemoveCallTargetDefinitions(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in RemoveCallTargetDefinitions call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->RemoveCallTargetDefinitions(id, items, size);
}

EXTERN_C VOID STDAPICALLTYPE EnableByRefInstrumentation()
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in EnableByRefInstrumentation call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->EnableByRefInstrumentation();
}

EXTERN_C VOID STDAPICALLTYPE EnableCallTargetStateByRef()
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in EnableCallTargetStateByRef call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->EnableCallTargetStateByRef();
}

EXTERN_C VOID STDAPICALLTYPE AddDerivedInstrumentations(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in AddDerivedInstrumentations call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->AddDerivedInstrumentations(id, items, size);
}

EXTERN_C VOID STDAPICALLTYPE AddInterfaceInstrumentations(WCHAR* id, trace::CallTargetDefinition* items, int size)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in AddInterfaceInstrumentations call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->AddInterfaceInstrumentations(id, items, size);
}

EXTERN_C VOID STDAPICALLTYPE AddTraceAttributeInstrumentation(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                                              WCHAR* integration_type_name_ptr)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in AddTraceAttributeInstrumentation call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->AddTraceAttributeInstrumentation(id, integration_assembly_name_ptr,
                                                             integration_type_name_ptr);
}

EXTERN_C VOID STDAPICALLTYPE InitializeTraceMethods(WCHAR* id, WCHAR* integration_assembly_name_ptr,
                                                    WCHAR* integration_type_name_ptr, WCHAR* configuration_string_ptr)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in InitializeTraceMethods call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->InitializeTraceMethods(id, integration_assembly_name_ptr, integration_type_name_ptr,
                                            configuration_string_ptr);
}

EXTERN_C VOID STDAPICALLTYPE InstrumentProbes(
    debugger::DebuggerMethodProbeDefinition* methodProbes, 
    int methodProbesLength, 
    debugger::DebuggerLineProbeDefinition* lineProbes, 
    int lineProbesLength,
    debugger::DebuggerMethodSpanProbeDefinition* spanProbes,
    int spanProbesLength,
    debugger::DebuggerRemoveProbesDefinition* revertProbes,
    int revertProbesLength)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in InstrumentProbes call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->InstrumentProbes(methodProbes, methodProbesLength, lineProbes, lineProbesLength, spanProbes, spanProbesLength, revertProbes, revertProbesLength);
}

EXTERN_C int STDAPICALLTYPE GetProbesStatuses(WCHAR** probeIds, int probeIdsLength, debugger::DebuggerProbeStatus* probeStatuses)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in GetProbesStatuses call. Tracer CLR Profiler was not initialized.");
        return 0;
    }

    return trace::profiler->GetProbesStatuses(probeIds, probeIdsLength, probeStatuses);
}

EXTERN_C VOID STDAPICALLTYPE DisableTracerCLRProfiler()
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in DisableTracerCLRProfiler call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->DisableTracerCLRProfiler();
}

EXTERN_C VOID STDAPICALLTYPE RegisterIastAspects(WCHAR** aspects, int aspectsLength)
{
    if (trace::profiler == nullptr)
    {
        trace::Logger::Error("Error in RegisterIastAspects call. Tracer CLR Profiler was not initialized.");
        return;
    }

    trace::profiler->RegisterIastAspects(aspects, aspectsLength);
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