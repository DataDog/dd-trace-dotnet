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

enum class DynamicInstrumentationErrorCode
{
    ALREADY_INSTRUMENTED = 0,
    NOT_SUPPORTED_METHOD_PROBE = 1,
    NOT_SUPPORTED_LINE_PROBE = 2,
    UNKNOWN_FAILURE_METHOD_PROBE = 3,
    UNKNOWN_FAILURE_LINE_PROBE = 4,
    CTOR_AND_CCTOR_NOT_SUPPORTED = 5,
    BYREF_LIKE_RETURN_NOT_SUPPORTED = 6,
    PROFILER_ASSEMBLY_NOT_LOADED_YET = 7,
    METHOD_IL_IMPORT_FAILURE = 8,
    LOCALS_PARSE_FAILURE = 9,
    ADD_DYNAMIC_INSTRUMENTATION_LOCALS_FAILURE = 10,
    BYREF_LIKE_TYPE_NOT_SUPPORTED = 11,
    GET_DEBUGGER_LOCALS_FAILURE = 12,
    METHOD_IL_EXPORT_FAILURE = 13,
    METHOD_IS_ASYNC_FAILURE = 14,
    TASK_RETURN_TYPE_RETRIEVAL_FAILURE = 15
};

const WSTRING general_error_message = WStr("Failed to instrument the method.");

inline std::wstring GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode errorCode)
{
    return general_error_message + L" [Error Code: " + std::to_wstring(static_cast<int>(errorCode)) + L"]";
}

const WSTRING invalid_probe_method_already_instrumented =
    WStr("The Dynamic Insturmentation has failed to place the probe because the corresponding method was previosuley "
         "instrumented by another product.");
const WSTRING invalid_method_probe_probe_is_not_supported =
    WStr("The method where the probe should have been placed is not supported for now.");
const WSTRING invalid_line_probe_probe_is_not_supported =
    WStr("The line where the probe should have been placed is not supported for now.");
const WSTRING invalid_probe_failed_to_instrument_method_probe = 
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::UNKNOWN_FAILURE_METHOD_PROBE);
const WSTRING invalid_probe_failed_to_instrument_line_probe =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::UNKNOWN_FAILURE_LINE_PROBE);
const WSTRING invalid_probe_probe_cctor_ctor_not_supported =
    WStr("Instrumentation of constructors are not supported for now.");
const WSTRING invalid_probe_probe_byreflike_return_not_supported =
    WStr("Instrumnetation of methods with Byref-like return type are not supported for now.");
const WSTRING profiler_assemly_is_not_loaded =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::PROFILER_ASSEMBLY_NOT_LOADED_YET);
const WSTRING invalid_probe_failed_to_import_method_il =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::METHOD_IL_IMPORT_FAILURE);
const WSTRING invalid_probe_failed_to_parse_locals =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::LOCALS_PARSE_FAILURE);
const WSTRING invalid_probe_failed_to_add_di_locals =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::ADD_DYNAMIC_INSTRUMENTATION_LOCALS_FAILURE);
const WSTRING invalid_probe_type_is_by_ref_like = WStr("Byref-like types are not supported for now.");
const WSTRING failed_to_get_debugger_locals =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::GET_DEBUGGER_LOCALS_FAILURE);
const WSTRING failed_to_export_method_il =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::METHOD_IL_EXPORT_FAILURE);
const WSTRING failed_to_determine_if_method_is_async =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::METHOD_IS_ASYNC_FAILURE);
const WSTRING failed_to_retrieve_task_return_type =
    GetDynamicInstrumentationErrorMessage(DynamicInstrumentationErrorCode::TASK_RETURN_TYPE_RETRIEVAL_FAILURE);

}

#endif // DEBUGGER_CONSTANTS_H
