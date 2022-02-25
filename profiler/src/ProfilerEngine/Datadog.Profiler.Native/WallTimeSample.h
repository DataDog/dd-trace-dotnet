// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "Sample.h"

class WallTimeSample : public Sample
{
public:
    WallTimeSample(
        uint64_t timestamp,
        uint64_t duration,
        uint64_t traceId,
        uint64_t spanId
        );

public:
    void SetPid(const std::string& pid);
    void SetAppDomainName(const std::string& name);
    void SetThreadId(const std::string& tid);
    void SetThreadName(const std::string& name);

public:
    static const std::string ThreadIdLabel;
    static const std::string ThreadNameLabel;
    static const std::string ProcessIdLabel;
    static const std::string AppDomainNameLabel;
};
