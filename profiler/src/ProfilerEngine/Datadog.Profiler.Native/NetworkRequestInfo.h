// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <chrono>
#include <string>

#include "Callstack.h"
#include "ManagedThreadInfo.h"


class NetworkRequestInfo
{
public:
    NetworkRequestInfo(std::string url, std::chrono::nanoseconds startTimestamp);

public:
    // request start
    std::chrono::nanoseconds StartTimestamp;
    std::string Url;
    uint64_t LocalRootSpanID;
    uint64_t SpanID;
    AppDomainID AppDomainId;
    Callstack StartCallStack;
    std::shared_ptr<ManagedThreadInfo> StartThreadInfo;
    std::chrono::nanoseconds ReqRespStartTime;
    std::chrono::nanoseconds ReqRespDuration;

    // DNS
    std::chrono::nanoseconds DnsStartTime;
    std::chrono::nanoseconds DnsDuration;
    bool DnsResolutionSuccess;

    // HTTPS
    std::chrono::nanoseconds HandshakeStartTime;
    std::chrono::nanoseconds HandshakeDuration;
    std::string HandshakeError;

    // socket connection
    std::chrono::nanoseconds SocketConnectStartTime;
    std::chrono::nanoseconds SocketDuration;

    // redirect
    std::string RedirectUrl;

    // failed request
    std::string Error;
};