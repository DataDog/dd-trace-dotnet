#pragma once

#include "TypeReference.h"
#include "IntegrationBase.h"

const struct
{
    const TypeReference System_Void = { ELEMENT_TYPE_VOID, L"mscorlib", L"System.Void" };
    const TypeReference System_Object = { ELEMENT_TYPE_OBJECT, L"mscorlib", L"System.Object" };
    const TypeReference System_Object_Array = { ELEMENT_TYPE_SZARRAY, L"mscorlib", L"System.Array", &System_Object };
    const TypeReference System_String = { ELEMENT_TYPE_STRING, L"mscorlib", L"System.String" };
    const TypeReference System_Int32 = { ELEMENT_TYPE_I4, L"mscorlib", L"System.Int32" };
    const TypeReference System_Int64 = { ELEMENT_TYPE_I4, L"mscorlib", L"System.Int64" };
    const TypeReference System_UInt32 = { ELEMENT_TYPE_U4, L"mscorlib", L"System.UInt32" };
    const TypeReference System_UInt64 = { ELEMENT_TYPE_U8, L"mscorlib", L"System.UInt64" };
    const TypeReference System_Boolean = { ELEMENT_TYPE_VALUETYPE, L"mscorlib", L"System.Boolean" };
    const TypeReference System_DateTime = { ELEMENT_TYPE_VALUETYPE, L"mscorlib", L"System.DateTime" };
    const TypeReference Datadog_Trace_ClrProfiler_Instrumentation = { ELEMENT_TYPE_CLASS, L"Datadog.Trace.ClrProfiler.Managed", L"Datadog.Trace.ClrProfiler.Instrumentation" };

    const std::vector<TypeReference> All = {
        System_Void,
        System_Object,
        System_String,
        System_Int32,
        System_Int64,
        System_UInt32,
        System_UInt64,
        System_Boolean,
        System_DateTime,
        Datadog_Trace_ClrProfiler_Instrumentation
    };
} GlobalTypeReferences;
