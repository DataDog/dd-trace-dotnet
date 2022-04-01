#pragma once

#include <string>

class IApplicationStore
{
public:
    virtual ~IApplicationStore() = default;

    virtual const std::string& GetName(std::string_view runtimeId) = 0;
};