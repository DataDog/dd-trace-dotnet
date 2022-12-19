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
    WStr("Datadog.Trace"),
    WStr("Newtonsoft.Json")};

const WSTRING skip_assemblies[]{WStr("mscorlib"),
                                WStr("netstandard"),
                                WStr("System.Configuration"),
                                WStr("Microsoft.AspNetCore.Razor.Language"),
                                WStr("Microsoft.AspNetCore.Mvc.RazorPages"),
                                WStr("Anonymously Hosted DynamicMethods Assembly"),
                                WStr("Datadog.AutoInstrumentation.ManagedLoader"),
                                WStr("ISymWrapper"),
                                WStr("Datadog.Trace"),
                                WStr("MSBuild")};

const WSTRING invalid_probe_method_already_instrumented = WStr("The Dynamic Insturmentation has failed to place the probe because the corresponding method was previosuley instrumented by another product.");
const WSTRING invalid_method_probe_probe_is_not_supported =
    WStr("The method where the probe should have been placed is not supported for now.");
const WSTRING invalid_line_probe_probe_is_not_supported =
    WStr("The line where the probe should have been placed is not supported for now.");
const WSTRING invalid_probe_failed_to_instrument_method_probe =
    WStr("There was an issue trying to instrument the method.");
const WSTRING invalid_probe_failed_to_instrument_line_probe =
    WStr("There was an issue trying to instrument the method.");
const WSTRING invalid_probe_probe_static_ctor_not_supported =
    WStr("Instrumentation of static constructors are not supported for now.");
const WSTRING invalid_probe_probe_byreflike_return_not_supported =
    WStr("Instrumnetation of methods with Byref-like return type are not supported for now.");
const WSTRING profiler_assemly_is_not_loaded =
    WStr("The profiler assembly has not loaded.");
const WSTRING invalid_probe_failed_to_import_method_il = WStr("The IL of the method has been failed to be imported.");
const WSTRING invalid_probe_failed_to_parse_locals = WStr("Failed to parse method locals.");
const WSTRING invalid_probe_failed_to_add_di_locals = WStr("Failed to add additional locals to the method.");
const WSTRING invalid_probe_type_is_by_ref_like = WStr("Byref-like types are not supported for now.");
const WSTRING failed_to_get_debugger_locals = WStr("Byref-like types are not supported for now.");
const WSTRING failed_to_export_method_il = WStr("Byref-like types are not supported for now.");
const WSTRING failed_to_determine_if_method_is_async = WStr("Byref-like types are not supported for now.");
const WSTRING failed_to_retrieve_task_return_type = WStr("Byref-like types are not supported for now.");
}

#endif // DEBUGGER_CONSTANTS_H
