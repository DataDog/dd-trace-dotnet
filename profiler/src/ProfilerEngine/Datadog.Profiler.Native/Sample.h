// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

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
typedef std::vector<std::pair<std::string_view, std::string_view>> CallStack;

/// <summary>
/// Unfinished class. The purpose, for now, is just to work on the export component.
/// </summary>
class Sample
{
public:
    static size_t ValuesCount;

public:
    Sample(std::string_view runtimeId); // only for tests
    Sample(uint64_t timestamp, std::string_view runtimeId, size_t framesCount);
    Sample& operator=(const Sample& sample) = delete;
    Sample(Sample&& sample) noexcept;
    Sample& operator=(Sample&& other) noexcept;

protected:
    Sample(const Sample&) = default;

public:
    Sample Copy() const;
    uint64_t GetTimeStamp() const;
    const Values& GetValues() const;
    const CallStack& GetCallstack() const;
    const Labels& GetLabels() const;
    std::string_view GetRuntimeId() const;

    // Since this class is not finished, this method is only for test purposes
    void SetValue(std::int64_t value);

    // should be protected if we want to derive classes from Sample such as WallTimeSample
    // but it seems better for encapsulation to do the transformation between collected raw data
    // and a Sample in each Provider (this is behind CollectorBase template class)
    void AddValue(std::int64_t value, size_t index);
    void AddFrame(std::string_view moduleName, std::string_view frame);

    template<typename T>
    void AddLabel(T&& label)
    {
        _labels.push_back(std::forward<T>(label));
    }

    // helpers for well known mandatory labels
    void SetPid(const std::string& pid);
    void SetAppDomainName(const std::string& name);
    void SetThreadId(const std::string& tid);
    void SetThreadName(const std::string& name);

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

private:
    uint64_t _timestamp;
    CallStack _callstack;
    Values _values;
    Labels _labels;
    std::string_view _runtimeId;
};