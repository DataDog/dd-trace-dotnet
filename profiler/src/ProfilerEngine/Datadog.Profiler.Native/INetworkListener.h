// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include <string>

class INetworkListener
{
public:
    virtual void OnRequestStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string url) = 0;
    virtual void OnRequestStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, uint32_t statusCode) = 0;
    virtual void OnRequestFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message) = 0;
    virtual void OnRedirect(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string redirectUrl) = 0;
    virtual void OnDnsResolutionStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;
    virtual void OnDnsResolutionStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, bool Success) = 0;
    virtual void OnConnectStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;
    virtual void OnConnectStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;
    virtual void OnConnectFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message) = 0;
    virtual void OnHandshakeStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string targetHost) = 0;
    virtual void OnHandshakeStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;
    virtual void OnHandshakeFailed(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, std::string message) = 0;
    virtual void OnRequestHeaderStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;
    virtual void OnResponseHeaderStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId, uint32_t statusCode) = 0;
    virtual void OnResponseContentStart(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;
    virtual void OnResponseContentStop(std::chrono::nanoseconds timestamp, LPCGUID pActivityId) = 0;

    virtual ~INetworkListener() = default;
};
