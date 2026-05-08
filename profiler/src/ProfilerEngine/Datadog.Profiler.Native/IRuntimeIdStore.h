// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <string>

#include "cor.h"
#include "corprof.h"

#include "IMemoryFootprintProvider.h"

class IRuntimeIdStore : public IMemoryFootprintProvider
{
public:
    virtual ~IRuntimeIdStore() = default;

    virtual const char* GetId(AppDomainID appDomainId) = 0;
};