#pragma once

#include "../../src/native-src/string.h"

class EnvironmentVariable
{
public:
    EnvironmentVariable(const ::shared::WSTRING& name, const ::shared::WSTRING& value);
    ~EnvironmentVariable();

private:
    ::shared::WSTRING _name;
    ::shared::WSTRING _value;
};