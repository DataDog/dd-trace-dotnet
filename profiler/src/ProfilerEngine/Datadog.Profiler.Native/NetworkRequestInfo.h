// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <string>

class NetworkRequestInfo
{
public:
    NetworkRequestInfo();

public:
    // HTTP request start
    std::string Url;
    uint64_t StartTimestamp;


    // HTTP request end
    uint64_t EndTimestamp;
    int32_t StatusCode;
};

