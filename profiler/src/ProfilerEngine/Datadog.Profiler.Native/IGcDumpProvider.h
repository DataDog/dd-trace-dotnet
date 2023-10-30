#pragma once

#include <string>
#include <vector>


class IGcDumpProvider
{
public:
    using typeInfo_t = std::tuple<std::string, uint64_t, uint64_t>;
    using gcdump_t = std::vector<typeInfo_t>;

public:
    virtual ~IGcDumpProvider() = default;

    virtual gcdump_t Get() = 0;
};