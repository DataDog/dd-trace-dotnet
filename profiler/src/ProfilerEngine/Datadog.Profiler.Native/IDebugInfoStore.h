// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

// from dotnet coreclr includes
#include "cor.h"
#include "corprof.h"
// end

#include <string_view>
#include <cstdint>

struct SymbolDebugInfo
{
public:
    std::string_view File;
    std::uint32_t StartLine = 0;
};

class IDebugInfoStore
{
public:
    virtual ~IDebugInfoStore() = default;
    virtual SymbolDebugInfo Get(ModuleID moduleId, mdMethodDef methodDef) = 0;
};