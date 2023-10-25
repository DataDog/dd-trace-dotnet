// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IFrameStore.h"

#include <array>
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
typedef std::pair<std::string_view, std::string> Label;
typedef std::list<Label> Labels;
typedef std::pair<std::string_view, int64_t> NumericLabel;
typedef std::list<NumericLabel> NumericLabels;
typedef std::vector<FrameInfoView> CallStack;

class Sample
{
public:
    static size_t ValuesCount;

public:
    Sample(std::string_view runtimeId); // only for tests
    Sample(uint64_t timestamp, std::string_view runtimeId, size_t framesCount);

    Sample(const Sample&) = delete;
    Sample& operator=(const Sample& sample) = delete;
    Sample(Sample&& sample) noexcept = delete;
    Sample& operator=(Sample&& other) noexcept = delete;

public:
    uint64_t GetTimeStamp() const;
    const Values& GetValues() const;
    const CallStack& GetCallstack() const;
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

    // well known labels
public:
    static const std::string ThreadIdLabel;
    static const std::string ThreadNameLabel;
    static const std::string ProcessIdLabel;
    static const std::string AppDomainNameLabel;
    static const std::string LocalRootSpanIdLabel;
    static const std::string SpanIdLabel;
    static const std::string ExceptionTypeLabel;
    static const std::string ExceptionMessageLabel;
    static const std::string AllocationClassLabel;
    static const std::string EndTimestampLabel;
    static const std::string GarbageCollectionGenerationLabel;
    static const std::string GarbageCollectionNumberLabel;
    static const std::string TimelineEventTypeLabel;
    static const std::string TimelineEventTypeStopTheWorld;
    static const std::string TimelineEventTypeGarbageCollection;
    static const std::string TimelineEventTypeThreadStart;
    static const std::string TimelineEventTypeThreadStop;
    static const std::string GarbageCollectionReasonLabel;
    static const std::string GarbageCollectionTypeLabel;
    static const std::string GarbageCollectionCompactingLabel;
    static const std::string ObjectLifetimeLabel;
    static const std::string ObjectIdLabel;
    static const std::string ObjectGenerationLabel;

private:
    uint64_t _timestamp;
    CallStack _callstack;
    Values _values;
    Labels _labels;
    NumericLabels _numericLabels;
    std::string_view _runtimeId;
};