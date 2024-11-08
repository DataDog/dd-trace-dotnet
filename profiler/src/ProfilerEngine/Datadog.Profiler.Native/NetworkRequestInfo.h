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
    // HTTP request start
    std::string Url;
    uint64_t StartTimestamp;
    uint64_t LocalRootSpanID;
    uint64_t SpanID;
    AppDomainID AppDomainId;
    Callstack StartCallStack;
    std::shared_ptr<ManagedThreadInfo> StartThreadInfo;

    // HTTP request end
    uint64_t EndTimestamp;
    int32_t StatusCode;
};

