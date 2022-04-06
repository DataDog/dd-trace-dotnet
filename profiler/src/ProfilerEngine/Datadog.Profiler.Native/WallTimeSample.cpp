// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "WallTimeSample.h"

// define label string constants
const std::string WallTimeSample::ThreadIdLabel        = "thread id";
const std::string WallTimeSample::ThreadNameLabel      = "thread name";
const std::string WallTimeSample::AppDomainNameLabel   = "appdomain name";
const std::string WallTimeSample::ProcessIdLabel       = "appdomain process id";
const std::string WallTimeSample::LocalRootSpanIdLabel = "local root span id";
const std::string WallTimeSample::SpanIdLabel          = "span id";

WallTimeSample::WallTimeSample(
    uint64_t timestamp,
    uint64_t duration,
    uint64_t localRootSpanId,
    uint64_t spanId)
    :
    Sample(timestamp)
{
    // set value
    AddValue(duration, SampleValue::WallTimeDuration);

    if (localRootSpanId != 0 && spanId != 0)
    {
        AddLabel(Label{LocalRootSpanIdLabel, std::to_string(localRootSpanId)});
        AddLabel(Label{SpanIdLabel, std::to_string(spanId)});
    }
}

void WallTimeSample::SetPid(const std::string& pid)
{
    AddLabel(Label{ProcessIdLabel, pid});
}

void WallTimeSample::SetAppDomainName(const std::string& name)
{
    AddLabel(Label{AppDomainNameLabel, name});
}

void WallTimeSample::SetThreadId(const std::string& tid)
{
    AddLabel(Label{ThreadIdLabel, tid});
}

void WallTimeSample::SetThreadName(const std::string& name)
{
    AddLabel(Label{ThreadNameLabel, name});
}
