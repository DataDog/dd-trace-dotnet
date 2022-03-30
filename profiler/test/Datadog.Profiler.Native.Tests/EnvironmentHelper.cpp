#include "EnvironmentHelper.h"
#include "shared/src/native-src/util.h"


void setenv(const shared::WSTRING& name, const shared::WSTRING& value)
{
#ifdef _WINDOWS
    SetEnvironmentVariable(name.c_str(), value.c_str());
#else
    setenv(shared::ToString(name).c_str(), shared::ToString(value).c_str(), 1);
#endif
}

void unsetenv(const shared::WSTRING& name)
{
#ifdef _WINDOWS
    SetEnvironmentVariable(name.c_str(), nullptr);
#else
    unsetenv(shared::ToString(name).c_str());
#endif
}

EnvironmentHelper::EnvironmentVariable::EnvironmentVariable(const ::shared::WSTRING& name, const ::shared::WSTRING& value) :
    _name{name},
    _value{value}
{
    setenv(_name, _value);
}

EnvironmentHelper::EnvironmentVariable::~EnvironmentVariable()
{
    unsetenv(_name);
}