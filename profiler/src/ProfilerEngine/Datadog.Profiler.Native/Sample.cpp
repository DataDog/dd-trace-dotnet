// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Sample.h"

Sample::Sample(uint64_t timestamp)
    : Sample()
{
    _timestamp = timestamp;
}

Sample::Sample()
{
    _timestamp = 0;
    _values = {0};
    _labels = {};
    _callstack = {};
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

const Labels& Sample::GetLabels() const
{
    return _labels;
}
