#pragma once

#include "shared/src/native-src/string.h"

class EnvironmentHelper
{
public:
    EnvironmentHelper() = delete;

    class EnvironmentVariable
    {
    public:
        EnvironmentVariable(const ::shared::WSTRING& name, const ::shared::WSTRING& value);
        ~EnvironmentVariable();

    private:
        ::shared::WSTRING _name;
        ::shared::WSTRING _value;
    };
};