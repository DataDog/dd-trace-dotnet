// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

#include "Callstack.h"
#include "ManagedThreadInfo.h"


class NetworkRequestInfo
{
public:
    NetworkRequestInfo(std::string url, uint64_t startTimestamp);

public:
    // request start
    uint64_t StartTimestamp;
    std::string Url;
    uint64_t LocalRootSpanID;
    uint64_t SpanID;
    AppDomainID AppDomainId;
    Callstack StartCallStack;
    std::shared_ptr<ManagedThreadInfo> StartThreadInfo;

    // DNS
    uint64_t DnsStartTime;
    uint64_t DnsDuration;
    bool DnsResolutionSuccess;

    // HTTPS
    uint64_t HandshakeStartTime;
    uint64_t HandshakeDuration;
    std::string HandshakeError;

    // socket connection
    uint64_t SocketConnectStartTime;
    uint64_t SocketDuration;

    // redirect
    std::string RedirectUrl;

    // failed request
    std::string Error;
};