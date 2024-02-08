// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"

#include "shared/src/native-src/string.h"

class IThreadInfo
{
public:
    virtual ~IThreadInfo() = default;
    virtual DWORD GetOsThreadId() const = 0;
    virtual shared::WSTRING const& GetThreadName() const = 0;
    virtual HANDLE GetOsThreadHandle() const = 0;

    // these 2 methods are only used for .NET Framework
    virtual std::string GetProfileThreadId() = 0;
    virtual std::string GetProfileThreadName() = 0;
};