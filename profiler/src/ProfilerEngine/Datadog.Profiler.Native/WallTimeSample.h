// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "Sample.h"

class WallTimeSample : public Sample
{
public:
    WallTimeSample(
        uint64_t timestamp,
        std::string_view runtimeId,
        uint64_t duration,
        uint64_t traceId,
        uint64_t spanId
        );

public:
    template <class TString>
    void SetPid(TString&& pid);

    template <class TString>
    void SetAppDomainName(TString&& name);

    template <class TString>
    void SetThreadId(TString&& tid);

    template <class TString>
    void SetThreadName(TString&& name);

public:
    static const std::string ThreadIdLabel;
    static const std::string ThreadNameLabel;
    static const std::string ProcessIdLabel;
    static const std::string AppDomainNameLabel;
    static const std::string LocalRootSpanIdLabel;
    static const std::string SpanIdLabel;
};

template <class TString>
void WallTimeSample::SetPid(TString&& pid)
{
    AddLabel(Label{ProcessIdLabel, std::forward<TString>(pid)});
}

template <class TString>
void WallTimeSample::SetAppDomainName(TString&& name)
{
    AddLabel(Label{AppDomainNameLabel, std::forward<TString>(name)});
}

template <class TString>
void WallTimeSample::SetThreadId(TString&& tid)
{
    AddLabel(Label{ThreadIdLabel, std::forward<TString>(tid)});
}

template <class TString>
void WallTimeSample::SetThreadName(TString&& name)
{
    AddLabel(Label{ThreadNameLabel, std::forward<TString>(name)});
}
