// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TimeSample.h"

// define label string constants
const std::string TimeSample::ThreadIdLabel = "thread id";
const std::string TimeSample::ThreadNameLabel = "thread name";
const std::string TimeSample::AppDomainNameLabel = "appdomain name";
const std::string TimeSample::ProcessIdLabel = "appdomain process id";
const std::string TimeSample::LocalRootSpanIdLabel = "local root span id";
const std::string TimeSample::SpanIdLabel = "span id";

TimeSample::TimeSample(
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

void TimeSample::SetPid(const std::string& pid)
{
    AddLabel(Label{ProcessIdLabel, pid});
}

void TimeSample::SetAppDomainName(const std::string& name)
{
    AddLabel(Label{AppDomainNameLabel, name});
}

void TimeSample::SetThreadId(const std::string& tid)
{
    AddLabel(Label{ThreadIdLabel, tid});
}

void TimeSample::SetThreadName(const std::string& name)
{
    AddLabel(Label{ThreadNameLabel, name});
}
