// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IFrameStore.h"

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

#include "Log.h"

extern "C" {
    #include "datadog/common.h"
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
typedef std::pair<ddog_prof_StringId, std::string> StringLabel;
typedef std::pair<ddog_prof_StringId, int64_t> NumericLabel;
typedef std::vector<NumericLabel> NumericLabels;
typedef std::vector<std::variant<StringLabel, NumericLabel>> Labels;

template<class... Ts>
struct LabelsVisitor : Ts... { using Ts::operator()...; };
template<class... Ts>
LabelsVisitor(Ts...) -> LabelsVisitor<Ts...>;

using namespace std::chrono_literals;

#include "SymbolsStore.h"


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
    const std::vector<MyFrameInfo>& GetCallstack() const;
    const Labels& GetLabels() const;
    std::string_view GetRuntimeId() const;

    // Since this class is not finished, this method is only for test purposes
    void SetValue(std::int64_t value);

    // should be protected if we want to derive classes from Sample such as WallTimeSample
    // but it seems better for encapsulation to do the transformation between collected raw data
    // and a Sample in each Provider (this is behind CollectorBase template class)
    void AddValue(std::int64_t value, size_t index);
    void AddFrame(MyFrameInfo frame);

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
                                   return elt != nullptr;// && elt->first == label.first;
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
        AddLabel(NumericLabel{_symbolsStore->GetProcessId(), std::forward<T>(pid)});
    }

    template <typename T>
    void SetAppDomainName(T&& name)
    {
        AddLabel(StringLabel{_symbolsStore->GetAppDomainName(), std::forward<T>(name)});
    }

    template <typename T>
    void SetThreadId(T&& tid)
    {
        AddLabel(StringLabel{_symbolsStore->GetThreadId(), std::forward<T>(tid)});
    }

    template <typename T>
    void SetThreadName(T&& name)
    {
        AddLabel(StringLabel{_symbolsStore->GetThreadName(), std::forward<T>(name)});
    }

    void SetTimestamp(std::chrono::nanoseconds timestamp)
    {
        _timestamp = timestamp;
    }

    void SetRuntimeId(std::string_view runtimeId)
    {
        _runtimeId = runtimeId;
    }

    void SetGroupingId(std::int64_t groupingId)
    {
        // Log::Warn("++++++++ grouping id ", groupingId);
        _groupingId = groupingId;
    }

    std::int64_t GetGroupingId() const
    {
        return _groupingId;
    }

    void Reset()
    {
        _timestamp = 0ns;
        _callstack.clear();
        _runtimeId = {};
        _allLabels.clear();
        _groupingId = -1;
        std::fill(_values.begin(), _values.end(), 0);
    }
    // well known labels
public:

private:
    std::chrono::nanoseconds _timestamp;
    std::vector<MyFrameInfo> _callstack;
    Values _values;
    Labels _allLabels;
    std::string_view _runtimeId;
    std::int64_t _groupingId;
    libdatadog::SymbolsStore* _symbolsStore;
};