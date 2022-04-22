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
    const std::string& Name;
    const std::string& Unit;
};


//---------------------------------------------------------------
// This array define the list of ALL values set by all profilers
SampleValueType const SampleTypeDefinitions[] =
{
    {"wall", "nanoseconds"},// WallTimeDuration
    {"cpu", "nanoseconds"}, // CPUTimeDuration

    // the new ones should be added here at the same time
    // new identifiers are added to SampleValue
};

// Each profiler defines its own values index in the array
// It will be used in the AddValue() method
//
enum class SampleValue : size_t
{
    // Wall time profiler
    WallTimeDuration = 0,

    // CPU time profiler
    CpuTimeDuration = 1,

    //// Allocation tick profiler
    //AllocationCount = 2,

    //// Thread contention profiler
    //ContentionCount = 3,
    //ContentionDuration = 4,

    //// Exception profiler
    //ExceptionCount = 5
};
//
static constexpr size_t array_size = sizeof(SampleTypeDefinitions) / sizeof(SampleTypeDefinitions[0]);
//---------------------------------------------------------------

typedef std::array<int64_t, array_size> Values;
typedef std::pair<std::string, std::string> Label;  // TODO: use stringview to avoid copy
typedef std::list<Label> Labels;


/// <summary>
/// Unfinished class. The purpose, for now, is just to work on the export component.
/// </summary>
class Sample
{
public:
    Sample(std::string_view runtimeId); // only for tests
    Sample(uint64_t timestamp, std::string_view runtimeId);
    Sample(const Sample&) = delete;
    Sample& operator=(const Sample& sample) = delete;
    Sample(Sample&& sample) noexcept;
    Sample& operator=(Sample&& other) noexcept;

public:
    uint64_t GetTimeStamp() const;
    const Values& GetValues() const;
    const std::vector<std::pair<std::string, std::string>>& GetCallstack() const;
    const Labels& GetLabels() const;
    std::string_view GetRuntimeId() const;

// Since this class is not finished, this method is only for test purposes
    void SetValue(std::int64_t value);

// should be protected if we want to derive classes from Sample such as WallTimeSample
// but it seems better for encapsulation to do the transformation between collected raw data
// and a Sample in each Provider (this is the each behind CollectorBase template class)
    void AddValue(std::int64_t value, SampleValue index);
    void AddFrame(const std::string& moduleName, const std::string& frame); // TODO: use stringview to avoid copy
    void AddLabel(const Label& label);

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

private:
    uint64_t _timestamp;
    std::vector<std::pair<std::string, std::string>> _callstack; // TODO: use stringview to avoid copy
    Values _values;
    Labels _labels;
    std::string_view _runtimeId;
};