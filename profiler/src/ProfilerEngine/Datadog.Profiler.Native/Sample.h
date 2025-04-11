// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IFrameStore.h"
#include "InternedString.h"

#include <array>
#include <chrono>
#include <iostream>
#include <list>
#include <string>
#include <string_view>
#include <tuple>
#include <vector>

struct SampleValueType
{
    std::string Name;
    std::string Unit;
};


typedef std::vector<int64_t> Values;
typedef std::pair<InternedString, std::string> Label;
typedef std::vector<Label> Labels;
typedef std::pair<InternedString, int64_t> NumericLabel;
typedef std::pair<InternedString, uint64_t> SpanLabel;
typedef std::vector<NumericLabel> NumericLabels;

using namespace std::chrono_literals;

class Sample
{
public:
    static size_t ValuesCount;

public:
    Sample(std::chrono::nanoseconds timestamp, std::string_view runtimeId, size_t framesCount);
    Sample(std::string_view runtimeId); // only for tests

#ifndef DD_TEST
private:
#endif
    // let compiler generating the move and copy ctors/assignment operators
    Sample(const Sample&) = default;
    Sample& operator=(const Sample& sample) = default;
    Sample(Sample&& sample) noexcept = default;
    Sample& operator=(Sample&& other) noexcept = default;

public:
    std::chrono::nanoseconds GetTimeStamp() const;
    const Values& GetValues() const;
    const std::vector<FrameInfoView>& GetCallstack() const;
    const Labels& GetLabels() const;
    const NumericLabels& GetNumericLabels() const;
    std::string_view GetRuntimeId() const;

    // Since this class is not finished, this method is only for test purposes
    void SetValue(std::int64_t value);

    // should be protected if we want to derive classes from Sample such as WallTimeSample
    // but it seems better for encapsulation to do the transformation between collected raw data
    // and a Sample in each Provider (this is behind CollectorBase template class)
    void AddValue(std::int64_t value, size_t index);
    void AddFrame(FrameInfoView const& frame);

    template<typename T>
    void AddLabel(T&& label)
    {
        _labels.push_back(std::forward<T>(label));
    }

    template<typename T>
    void AddNumericLabel(T&& label)
    {
        _numericLabels.push_back(std::forward<T>(label));
    }

    template<typename T>
    void ReplaceLabel(T&& label)
    {
        for (auto it = _labels.rbegin(); it != _labels.rend(); it++)
        {
            if (it->first == label.first)
            {
                it->second = label.second;

                return;
            }
        }
    }

    template<typename T>
    void ReplaceNumericLabel(T&& label)
    {
        for (auto it = _numericLabels.rbegin(); it != _numericLabels.rend(); it++)
        {
            if (it->first == label.first)
            {
                it->second = label.second;

                return;
            }
        }
    }

    // helpers for well known mandatory labels
    template <typename T>
    void SetPid(T&& pid)
    {
        AddNumericLabel(NumericLabel{ProcessIdLabel, std::forward<T>(pid)});
    }

    template <typename T>
    void SetAppDomainName(T&& name)
    {
        AddLabel(Label{AppDomainNameLabel, std::forward<T>(name)});
    }

    template <typename T>
    void SetThreadId(T&& tid)
    {
        AddLabel(Label{ThreadIdLabel, std::forward<T>(tid)});
    }

    template <typename T>
    void SetThreadName(T&& name)
    {
        AddLabel(Label{ThreadNameLabel, std::forward<T>(name)});
    }

    void SetTimestamp(std::chrono::nanoseconds timestamp)
    {
        _timestamp = timestamp;
    }

    void SetRuntimeId(std::string_view runtimeId)
    {
        _runtimeId = runtimeId;
    }

    void Reset()
    {
        _timestamp = 0ns;
        _callstack.clear();
        _runtimeId = {};
        _numericLabels.clear();
        _labels.clear();
        std::fill(_values.begin(), _values.end(), 0);
    }
    // well known labels
public:
    static const InternedString ThreadIdLabel;
    static const InternedString ThreadNameLabel;
    static const InternedString ProcessIdLabel;
    static const InternedString AppDomainNameLabel;
    static const InternedString LocalRootSpanIdLabel;
    static const InternedString SpanIdLabel;
    static const std::string ExceptionTypeLabel;
    static const InternedString ExceptionMessageLabel;
    static const InternedString AllocationClassLabel;
    static const InternedString GarbageCollectionGenerationLabel;
    static const InternedString GarbageCollectionNumberLabel;
    static const InternedString TimelineEventTypeLabel;
    static const std::string TimelineEventTypeStopTheWorld;
    static const std::string TimelineEventTypeGarbageCollection;
    static const std::string TimelineEventTypeThreadStart;
    static const std::string TimelineEventTypeThreadStop;
    static const InternedString GarbageCollectionReasonLabel;
    static const InternedString GarbageCollectionTypeLabel;
    static const InternedString GarbageCollectionCompactingLabel;
    static const InternedString ObjectLifetimeLabel;
    static const InternedString ObjectIdLabel;
    static const InternedString ObjectGenerationLabel;
    static const InternedString RequestUrlLabel;
    static const InternedString RequestStatusCodeLabel;
    static const InternedString RequestErrorLabel;
    static const InternedString RequestRedirectUrlLabel;
    static const InternedString RequestDnsWaitLabel;
    static const InternedString RequestDnsDurationLabel;
    static const InternedString RequestDnsSuccessLabel;
    static const InternedString RequestHandshakeWaitLabel;
    static const InternedString RequestHandshakeDurationLabel;
    static const InternedString RequestHandshakeErrorLabel;
    static const InternedString RequestSocketDurationLabel;
    static const InternedString RequestResponseThreadIdLabel;
    static const InternedString RequestResponseThreadNameLabel;
    static const InternedString RequestDurationLabel;
    static const InternedString ResponseContentDurationLabel;

private:
    std::chrono::nanoseconds _timestamp;
    std::vector<FrameInfoView> _callstack;
    Values _values;
    Labels _labels;
    NumericLabels _numericLabels;
    std::string_view _runtimeId;
};