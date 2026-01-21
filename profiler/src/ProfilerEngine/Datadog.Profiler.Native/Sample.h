// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IFrameStore.h"

#include "SymbolsStore.h"

#include <algorithm>
#include <array>
#include <chrono>
#include <iostream>
#include <list>
#include <string>
#include <string_view>
#include <tuple>
#include <variant>
#include <vector>

// forward declaration
namespace libdatadog {
struct StringId;
}

struct SampleValueType
{
    std::string Name;
    std::string Unit;

    // Samples belonging to the same provider will share the same index
    // For libdatadog, it means that they will be stored in the same profile
    // This value will be set when registering the SampleValueType with SampleValueTypeProvider
    int32_t Index; // -1 means not set
};

typedef std::vector<int64_t> Values;
typedef std::pair<libdatadog::StringId*, std::string> StringLabel;
typedef std::pair<libdatadog::StringId*, int64_t> NumericLabel;
typedef std::vector<NumericLabel> NumericLabels;
typedef std::vector<std::variant<StringLabel, NumericLabel>> Labels;

template<class... Ts>
struct LabelsVisitor : Ts... { using Ts::operator()...; };
template<class... Ts>
LabelsVisitor(Ts...) -> LabelsVisitor<Ts...>;

using namespace std::chrono_literals;

class Sample final
{
public:
    static size_t ValuesCount;

public:
    Sample(std::chrono::nanoseconds timestamp, std::string_view runtimeId, size_t framesCount, libdatadog::SymbolsStore* symbolsStore);
    Sample(std::string_view runtimeId, libdatadog::SymbolsStore* symbolsStore); // only for tests

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
    std::string_view GetRuntimeId() const;

    // Since this class is not finished, this method is only for test purposes
    void SetValue(std::int64_t value);

    // should be protected if we want to derive classes from Sample such as WallTimeSample
    // but it seems better for encapsulation to do the transformation between collected raw data
    // and a Sample in each Provider (this is behind CollectorBase template class)
    void AddValue(std::int64_t value, size_t index);
    void AddFrame(FrameInfoView const& frame);

    template <typename T>
    void AddLabel(T&& label)
    {
        _allLabels.push_back(std::forward<T>(label));
    }

    template <typename T>
    void ReplaceLabel(T&& label)
    {
        auto it = std::find_if(_allLabels.rbegin(), _allLabels.rend(),
                               [&label](auto& item) {
                                   T* elt = std::get_if<T>(&item);
                                   return elt != nullptr && elt->first == label.first;
                               });

        if (it != _allLabels.rend())
        {
            auto& e = std::get<T>(*it);
            e.second = label.second;
        }
    }

    // helpers for well known mandatory labels
    template <typename T>
    void SetPid(T&& pid)
    {
        AddLabel(NumericLabel{_pSymbolsStore->GetProcessId(), std::forward<T>(pid)});
    }

    template <typename T>
    void SetAppDomainName(T&& name)
    {
        AddLabel(StringLabel{_pSymbolsStore->GetAppDomainName(), std::forward<T>(name)});
    }

    template <typename T>
    void SetThreadId(T&& tid)
    {
        AddLabel(StringLabel{_pSymbolsStore->GetThreadId(), std::forward<T>(tid)});
    }

    template <typename T>
    void SetThreadName(T&& name)
    {
        AddLabel(StringLabel{_pSymbolsStore->GetThreadName(), std::forward<T>(name)});
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
        _allLabels.clear();
        std::fill(_values.begin(), _values.end(), 0);
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
    static const std::string RequestUrlLabel;
    static const std::string RequestStatusCodeLabel;
    static const std::string RequestErrorLabel;
    static const std::string RequestRedirectUrlLabel;
    static const std::string RequestDnsWaitLabel;
    static const std::string RequestDnsDurationLabel;
    static const std::string RequestDnsSuccessLabel;
    static const std::string RequestHandshakeWaitLabel;
    static const std::string RequestHandshakeDurationLabel;
    static const std::string RequestHandshakeErrorLabel;
    static const std::string RequestSocketDurationLabel;
    static const std::string RequestResponseThreadIdLabel;
    static const std::string RequestResponseThreadNameLabel;
    static const std::string RequestDurationLabel;
    static const std::string ResponseContentDurationLabel;

private:
    std::chrono::nanoseconds _timestamp;
    std::vector<FrameInfoView> _callstack;
    Values _values;
    Labels _allLabels;
    std::string_view _runtimeId;
    libdatadog::SymbolsStore* _pSymbolsStore;
};