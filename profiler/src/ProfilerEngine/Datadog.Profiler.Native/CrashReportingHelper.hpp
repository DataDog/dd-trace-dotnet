// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.


extern "C"
{
#include "datadog/common.h"
}



template <typename T>
struct always_false : std::false_type {};

template <typename T>
bool Succeeded(T value)
{
    if constexpr(std::is_same_v<T, ddog_VoidResult_Tag>)
    {
        return value == DDOG_VOID_RESULT_OK;
    }
    else if constexpr(std::is_same_v<T, ddog_crasht_CrashInfoBuilder_NewResult_Tag>)
    {
        return value == DDOG_CRASHT_CRASH_INFO_BUILDER_NEW_RESULT_OK;
    }
    else if constexpr(std::is_same_v<T, ddog_crasht_StackFrame_NewResult_Tag>)
    {
        return value == DDOG_CRASHT_STACK_FRAME_NEW_RESULT_OK;
    }
    else if constexpr (std::is_same_v<T, ddog_crasht_StackTrace_NewResult_Tag>)
    {
        return value == DDOG_CRASHT_STACK_TRACE_NEW_RESULT_OK;
    }
    else if constexpr (std::is_same_v<T, ddog_crasht_Result_HandleCrashInfo_Tag>)
    {
        return value == DDOG_CRASHT_RESULT_HANDLE_CRASH_INFO_OK_HANDLE_CRASH_INFO;
    }
    else if constexpr (std::is_same_v<T, ddog_Vec_Tag_PushResult_Tag>)
    {
        return value == DDOG_VEC_TAG_PUSH_RESULT_OK;
    }
    else
    {
        static_assert(always_false<T>::value, "unknwn type");
    }
    return false;
}

template <typename T>
std::pair<decltype(T::ok), bool> CrashReporting::ExtractResult(T v)
{
    if (!Succeeded(v.tag)){
        SetLastError(v.err);
        return {{}, false};
    }

    return {v.ok, true};
}

#define CHECK_RESULT(stmt)            \
    do                                \
    {                                 \
        auto result = stmt;           \
        if (!Succeeded(result.tag))   \
        {                             \
            SetLastError(result.err); \
            return 1;                 \
        }                             \
    } while (0);
