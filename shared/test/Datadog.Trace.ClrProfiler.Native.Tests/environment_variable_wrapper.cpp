#include "environment_variable_wrapper.h"
#include "../../src/native-src/util.h"

EnvironmentVariable::EnvironmentVariable(const ::shared::WSTRING& name, const ::shared::WSTRING& value) :
    _name{name},
    _value{value}
{
    ::shared::SetEnvironmentValue(_name, _value);
}

EnvironmentVariable::~EnvironmentVariable()
{
    ::shared::UnsetEnvironmentValue(_name);
}