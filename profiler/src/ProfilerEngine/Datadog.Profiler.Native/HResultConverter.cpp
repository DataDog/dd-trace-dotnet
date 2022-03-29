// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "HResultConverter.h"

#include <sstream>
#include <string>

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

const char* HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_ABORTED = "CORPROF_E_STACKSNAPSHOT_ABORTED";
const char* HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD = "CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD";
const char* HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX = "CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX";
const char* HResultConverter::hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNSAFE = "CORPROF_E_STACKSNAPSHOT_UNSAFE";
const char* HResultConverter::hrCodeNameStr_CORPROF_E_INCONSISTENT_WITH_FLAGS = "CORPROF_E_INCONSISTENT_WITH_FLAGS";
const char* HResultConverter::hrCodeNameStr_CORPROF_E_UNSUPPORTED_CALL_SEQUENCE = "CORPROF_E_UNSUPPORTED_CALL_SEQUENCE";
const char* HResultConverter::hrCodeNameStr_E_INVALIDARG = "E_INVALIDARG";
const char* HResultConverter::hrCodeNameStr_E_FAIL = "E_FAIL";
const char* HResultConverter::hrCodeNameStr_S_FALSE = "S_FALSE";
const char* HResultConverter::hrCodeNameStr_S_OK = "S_OK";
const char* HResultConverter::hrCodeNameStr_UnspecifiedFail = "Unspecified-Failure";
const char* HResultConverter::hrCodeNameStr_UnspecifiedSuccess = "Unspecified-Success";
const char* HResultConverter::hrCodeNameStr_UnspecifiedUnknown = "Unspecified-Unknown";

const char* HResultConverter::ToChars(HRESULT hr)
{
    switch (hr)
    {
        case CORPROF_E_STACKSNAPSHOT_ABORTED:
            return hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_ABORTED;

        case CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD:
            return hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_INVALID_TGT_THREAD;

        case CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX:
            return hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNMANAGED_CTX;

        case CORPROF_E_STACKSNAPSHOT_UNSAFE:
            return hrCodeNameStr_CORPROF_E_STACKSNAPSHOT_UNSAFE;

        case CORPROF_E_INCONSISTENT_WITH_FLAGS:
            return hrCodeNameStr_CORPROF_E_INCONSISTENT_WITH_FLAGS;

        case CORPROF_E_UNSUPPORTED_CALL_SEQUENCE:
            return hrCodeNameStr_CORPROF_E_UNSUPPORTED_CALL_SEQUENCE;

        case E_INVALIDARG:
            return hrCodeNameStr_E_INVALIDARG;

        case E_FAIL:
            return hrCodeNameStr_E_FAIL;

        case S_FALSE:
            return hrCodeNameStr_S_FALSE;

        case S_OK:
            return hrCodeNameStr_S_OK;

        default:
            if (FAILED(hr))
            {

                return hrCodeNameStr_UnspecifiedFail;
            }

            if (SUCCEEDED(hr))
            {

                return hrCodeNameStr_UnspecifiedSuccess;
            }

            return hrCodeNameStr_UnspecifiedUnknown;
    }
}

std::string HResultConverter::ToStringWithCode(HRESULT hr)
{
    std::ostringstream oss;
    oss << ToChars(hr) << " (" << std::hex << hr << ")";
    return oss.str();
}
