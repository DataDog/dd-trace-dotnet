// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <mutex>
#include <list>

#include "ISamplesProvider.h"
#include "Sample.h"


class ProviderBase : public ISamplesProvider
{
public:
    ProviderBase(const char* name);
    const char* GetName() override;

protected:
    std::string _name;
};
