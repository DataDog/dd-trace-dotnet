// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "cor.h"
#include "corprof.h"
#include <string>

class IAppDomainStore
{
public:
    virtual ~IAppDomainStore() = default;

    virtual bool GetInfo(AppDomainID appDomainId, ProcessID& pid, std::string& appDomainName) = 0;
};
