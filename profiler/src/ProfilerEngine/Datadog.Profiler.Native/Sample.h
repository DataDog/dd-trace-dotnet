// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <array>
#include <iostream>
#include <list>
#include <string>
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
    {"wall", "nanoseconds"}, // WallTimeDuration

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

    //// Allocation tick profiler
    //AllocationCount = 1,

    //// Thread contention profiler
    //ContentionCount = 2,
    //ContentionDuration = 3,

    //// Exception profiler
    //ExceptionCount = 4
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
    Sample();   // only for tests
    Sample(uint64_t timestamp);
    Sample(const Sample&) = delete;
    Sample& operator=(const Sample& sample) = delete;
    Sample(Sample&& sample) noexcept;
    Sample& operator=(Sample&& other) noexcept;

public:
    uint64_t GetTimeStamp() const;
    const Values& GetValues() const;
    const std::vector<std::pair<std::string, std::string>>& GetCallstack() const;
    const Labels& GetLabels() const;

// Since this class is not finished, this method is only for test purposes
    void SetValue(std::int64_t value);

// should be protected
    void AddValue(std::int64_t value, SampleValue index);
    void AddFrame(const std::string& moduleName, const std::string& frame); // TODO: use stringview to avoid copy
    void AddLabel(const Label& label);

private:
    uint64_t _timestamp;
    std::vector<std::pair<std::string, std::string>> _callstack; // TODO: use stringview to avoid copy
    Values _values;
    Labels _labels;
};