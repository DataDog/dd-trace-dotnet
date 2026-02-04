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

    virtual std::string_view GetName(AppDomainID appDomainId) = 0;
    virtual void Register(AppDomainID appDomainId) = 0;
};
