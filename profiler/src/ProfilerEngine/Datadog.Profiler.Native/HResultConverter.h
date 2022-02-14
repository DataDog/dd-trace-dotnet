// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>
#include <winerror.h>

class HResultConverter
{
public:
    static const char* hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_ABORTED;
    static const char* hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD;
    static const char* hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX;
    static const char* hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNSAFE;
    static const char* hrCodeNameStr_CORPROF_E_INCONSISTENT_WITH_FLAGS;
    static const char* hrCodeNameStr_CORPROF_E_UNSUPPORTED_CALL_SEQUENCE;
    static const char* hrCodeNameStr_E_INVALIDARG;
    static const char* hrCodeNameStr_E_FAIL;
    static const char* hrCodeNameStr_S_FALSE;
    static const char* hrCodeNameStr_S_OK;
    static const char* hrCodeNameStr_UnspecifiedFail;
    static const char* hrCodeNameStr_UnspecifiedSuccess;
    static const char* hrCodeNameStr_UnspecifiedUnknown;

public:
    static const char* ToChars(HRESULT hr);
    static std::string ToStringWithCode(HRESULT hr);
};
