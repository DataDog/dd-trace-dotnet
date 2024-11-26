// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include <string>

class INetworkListener
{
public:
    virtual void OnRequestStart(uint64_t timestamp, LPCGUID pActivityId, std::string url) = 0;
    virtual void OnRequestStop(uint64_t timestamp, LPCGUID pActivityId, uint32_t statusCode) = 0;
    virtual void OnRequestFailed(uint64_t timestamp, LPCGUID pActivityId, std::string message) = 0;
    virtual void OnRedirect(uint64_t timestamp, LPCGUID pActivityId, std::string redirectUrl) = 0;
    virtual void OnDnsResolutionStart(uint64_t timestamp, LPCGUID pActivityId) = 0;
    virtual void OnDnsResolutionStop(uint64_t timestamp, LPCGUID pActivityId, bool Success) = 0;
    virtual void OnConnectStart(uint64_t timestamp, LPCGUID pActivityId) = 0;
    virtual void OnConnectStop(uint64_t timestamp, LPCGUID pActivityId) = 0;
    virtual void OnConnectFailed(uint64_t timestamp, LPCGUID pActivityId, std::string message) = 0;



    virtual ~INetworkListener() = default;
};
