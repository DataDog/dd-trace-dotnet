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
    WStr("Newtonsoft.Json"),
    WStr("xunit")};

const WSTRING skip_assemblies[]{WStr("mscorlib"),
                                WStr("netstandard"),
                                WStr("System.Configuration"),
                                WStr("Microsoft.AspNetCore.Razor.Language"),
                                WStr("Microsoft.AspNetCore.Mvc.RazorPages"),
                                WStr("Anonymously Hosted DynamicMethods Assembly"),
                                WStr("Datadog.AutoInstrumentation.ManagedLoader"),
                                WStr("ISymWrapper"),
                                WStr("Datadog.Trace"),
                                WStr("MSBuild"),
                                WStr("MySql.Data")};

const WSTRING dynamic_span_operation_name = WStr("dd.dynamic.span");

const WSTRING general_error_message = WStr("Failed to instrument the method.");

inline WSTRING GetGenericErrorMessageWithErrorCode(short errorCode)
{
    return general_error_message + WStr(" [Error Code: ") + shared::ToWSTRING(errorCode) + WStr("]");
}

const WSTRING invalid_probe_method_already_instrumented =
    WStr("Dynamic Instrumentation failed to install the probe because the corresponding method is already instrumented by another product.");
const WSTRING invalid_method_probe_probe_is_not_supported =
    WStr("The method where the probe should have been placed is not supported.");
const WSTRING line_probe_il_offset_lookup_failure =
    WStr("There was a failure in determining the exact location where the line probe was supposed to be placed.");
const WSTRING line_probe_il_offset_lookup_failure_2 =
    WStr("There was a failure in determining the exact location where the line probe was supposed to be placed. [2]");
const WSTRING line_probe_in_async_generic_method_in_optimized_code =
    WStr("Placing line probes in async generic methods in Release builds is currently not supported.");
const WSTRING invalid_probe_probe_cctor_ctor_not_supported =
    WStr("Instrumentation of static/non-static constructors is not supported.");
const WSTRING invalid_probe_probe_byreflike_return_not_supported =
    WStr("Dynamic Instrumentation of methods that return a `ref struct` is not yet supported.");
const WSTRING invalid_probe_type_is_by_ref_like =
    WStr("Dynamic Instrumentation of methods in a `ref-struct` is not yet supported.");
const WSTRING non_supported_compiled_bytecode =
    WStr("Compiled code with `tail` call is not yet supported (F#).");
const WSTRING type_contains_invalid_symbol = 
    WStr("The type is not supported.");
const WSTRING async_method_could_not_load_this = WStr("Instrumentation of async method in a generic class is not yet supported.");
const WSTRING invalid_probe_failed_to_instrument_method_probe = 
    GetGenericErrorMessageWithErrorCode(1);
const WSTRING invalid_probe_failed_to_instrument_line_probe = 
    GetGenericErrorMessageWithErrorCode(2);
const WSTRING profiler_assemly_is_not_loaded =
    GetGenericErrorMessageWithErrorCode(3);
const WSTRING invalid_probe_failed_to_import_method_il =
    GetGenericErrorMessageWithErrorCode(4);
const WSTRING invalid_probe_failed_to_parse_locals =
    GetGenericErrorMessageWithErrorCode(5);
const WSTRING invalid_probe_failed_to_add_di_locals =
    GetGenericErrorMessageWithErrorCode(6);
const WSTRING failed_to_get_debugger_locals =
    GetGenericErrorMessageWithErrorCode(7);
const WSTRING failed_to_export_method_il =
    GetGenericErrorMessageWithErrorCode(8);
const WSTRING failed_to_determine_if_method_is_async =
    GetGenericErrorMessageWithErrorCode(9);
const WSTRING failed_to_retrieve_task_return_type =
    GetGenericErrorMessageWithErrorCode(10);

}

#endif // DEBUGGER_CONSTANTS_H
