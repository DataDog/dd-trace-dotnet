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


Sample::Sample(uint64_t timestamp, std::string_view runtimeId) :
    Sample(runtimeId)
{
    _timestamp = timestamp;
    _runtimeId = runtimeId;
}

Sample::Sample(std::string_view runtimeId)
{
    _timestamp = 0;
    _values = {0};
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

void Sample::AddValue(std::int64_t value, SampleValue index)
{
    size_t pos = static_cast<size_t>(index);
    if (pos >= array_size)
    {
        // TODO: fix compilation error about std::stringstream
        //std::stringstream builder;
        //builder << "\"index\" (=" << index << ") is greater than limit (=" << array_size << ")";
        //throw std::invalid_argument(builder.str());
        throw std::invalid_argument("index");
    }

    _values[pos] = value;
}

void Sample::AddFrame(const std::string& moduleName, const std::string& frame)
{
    _callstack.push_back({ moduleName, frame });
}

const std::vector<std::pair<std::string, std::string>>& Sample::GetCallstack() const
{
    return _callstack;
}

void Sample::AddLabel(const Label& label)
{
    _labels.push_back(label);
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
