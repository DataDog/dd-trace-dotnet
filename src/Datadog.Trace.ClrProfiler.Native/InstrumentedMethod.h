#pragma once

#include <string>
#include "TypeReference.h"

struct InstrumentedMethod
{
    std::wstring ModuleName = L"";
    std::wstring TypeName = L"";
    std::wstring MethodName = L"";
    TypeReference ReturnType{};
};
