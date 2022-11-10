// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Sample.h"

// define well known label string constants
const std::string Sample::ThreadIdLabel = "thread id";
const std::string Sample::ThreadNameLabel = "thread name";
const std::string Sample::AppDomainNameLabel = "appdomain name";
const std::string Sample::ProcessIdLabel = "appdomain process id";
const std::string Sample::LocalRootSpanIdLabel = "local root span id";
const std::string Sample::SpanIdLabel = "span id";
const std::string Sample::ExceptionTypeLabel = "exception type";
const std::string Sample::ExceptionMessageLabel = "exception message";
const std::string Sample::AllocationClassLabel = "allocation class";
const std::string Sample::ObjectAllocationTimeLabel = "allocation time";

// garbage collection related labels
const std::string Sample::TimelineEventTypeLabel = "event";
    const std::string Sample::TimelineEventTypeStopTheWorld = "stw";
    const std::string Sample::TimelineEventTypeGarbageCollection = "gc";
        const std::string Sample::GarbageCollectionReasonLabel = "gc reason";   // look at GCReason enumeration
        const std::string Sample::GarbageCollectionTypeLabel = "gc type";       // look at GCType enumeration
        const std::string Sample::GarbageCollectionCompactingLabel = "gc compacting"; // true or false
const std::string Sample::GarbageCollectionGenerationLabel = "gc generation";
const std::string Sample::GarbageCollectionNumberLabel = "gc number";


// TODO: update the values vector size if more than 16 slots are needed
size_t Sample::ValuesCount = 16;  // should be set BEFORE any sample gets created


Sample::Sample(uint64_t timestamp, std::string_view runtimeId, size_t framesCount) :
    Sample(runtimeId)
{
    _timestamp = timestamp;
    _runtimeId = runtimeId;
    _callstack.reserve(framesCount);
}

Sample::Sample(std::string_view runtimeId)
    :
    _values(ValuesCount)
{
    _timestamp = 0;
    _labels = {};
    _callstack = {};
    _runtimeId = runtimeId;
}

Sample::Sample(Sample&& sample) noexcept
{
    *this = std::move(sample);
}

Sample& Sample::operator=(Sample&& other) noexcept
{
    _timestamp = other._timestamp;
    _callstack = std::move(other._callstack);
    _values = std::move(other._values);
    _labels = std::move(other._labels);
    _runtimeId = other._runtimeId;

    return *this;
}

Sample Sample::Copy() const
{
    return {*this};
}

uint64_t Sample::GetTimeStamp() const
{
    return _timestamp;
}

const Values& Sample::GetValues() const
{
    return _values;
}

/// <summary>
/// Since this class is not finished, this method is only for test purposes
/// </summary>
/// <param name="value"></param>
void Sample::SetValue(std::int64_t value)
{
    _values[0] = value;
}

void Sample::AddValue(std::int64_t value, size_t index)
{
    if (index >= ValuesCount)
    {
        // TODO: fix compilation error about std::stringstream
        // std::stringstream builder;
        // builder << "\"index\" (=" << index << ") is greater than limit (=" << array_size << ")";
        // throw std::invalid_argument(builder.str());
        throw std::invalid_argument("index");
    }

    _values[index] = value;
}

void Sample::AddFrame(std::string_view moduleName, std::string_view frame)
{
    _callstack.push_back({moduleName, frame});
}

const CallStack& Sample::GetCallstack() const
{
    return _callstack;
}

std::string_view Sample::GetRuntimeId() const
{
    return _runtimeId;
}

const Labels& Sample::GetLabels() const
{
    return _labels;
}

void Sample::SetPid(const std::string& pid)
{
    AddLabel(Label{ProcessIdLabel, pid});
}

void Sample::SetAppDomainName(const std::string& name)
{
    AddLabel(Label{AppDomainNameLabel, name});
}

void Sample::SetThreadId(const std::string& tid)
{
    AddLabel(Label{ThreadIdLabel, tid});
}

void Sample::SetThreadName(const std::string& name)
{
    AddLabel(Label{ThreadNameLabel, name});
}
