#ifndef DEBUGGER_CONSTANTS_H
#define DEBUGGER_CONSTANTS_H

#include "cor_profiler.h"

using namespace trace;
using namespace shared;

namespace debugger
{

const WSTRING skip_assembly_prefixes[]{
    WStr("System"),
    WStr("Microsoft"),
    WStr("Windows"),
    WStr("runtime"),
    WStr("RefEmit_"),
    WStr("vstest"),
    WStr("testhost"),
    WStr("dotnet"),
    WStr("SOS"),
    WStr("NuGet"),
    WStr("VBCSCompiler"),
    WStr("csc"),
    WStr("DuckTypeNotVisibleAssembly"),
    WStr("Datadog.Trace")};

const WSTRING skip_assemblies[]{WStr("mscorlib"),
                                WStr("netstandard"),
                                WStr("System.Configuration"),
                                WStr("Microsoft.AspNetCore.Razor.Language"),
                                WStr("Microsoft.AspNetCore.Mvc.RazorPages"),
                                WStr("Anonymously Hosted DynamicMethods Assembly"),
                                WStr("Datadog.AutoInstrumentation.ManagedLoader"),
                                WStr("ISymWrapper"),
                                WStr("Datadog.Trace")};

const shared::WSTRING debugger_iasync_state_machine_name = WStr("System.Runtime.CompilerServices.IAsyncStateMachine");

}

#endif // DEBUGGER_CONSTANTS_H
