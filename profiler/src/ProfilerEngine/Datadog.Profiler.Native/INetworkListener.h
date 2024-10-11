// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

class INetworkListener
{
public:
    virtual void OnRequest(uint64_t timestamp, std::string url) = 0;

    virtual ~INetworkListener() = default;
};