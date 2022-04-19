// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IService.h"

#include <string>

class IApplicationStore : public IService
{
public:
    virtual const std::string& GetServiceName(std::string_view runtimeId) = 0;

    virtual void SetApplicationInfo(std::string runtimeId, std::string serviceName, std::string environment, std::string version) = 0;
};