// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Sample.h"

#include "SymbolsStore.h"

// TODO: update the values vector size if more than 16 slots are needed
size_t Sample::ValuesCount = 16;  // should be set BEFORE any sample gets created


Sample::Sample(std::chrono::nanoseconds timestamp, std::string_view runtimeId, size_t framesCount, libdatadog::SymbolsStore* symbolsStore) :
    Sample(runtimeId, symbolsStore)
{
    _timestamp = timestamp;
    _runtimeId = runtimeId;
    _callstack.reserve(framesCount);
    _allLabels.reserve(10);
}

Sample::Sample(std::string_view runtimeId, libdatadog::SymbolsStore* symbolsStore) :
    _values(ValuesCount),
    _timestamp{0},
    _allLabels{},
    _callstack{},
    _runtimeId{runtimeId},
    _symbolsStore{symbolsStore}
{
}

std::chrono::nanoseconds Sample::GetTimeStamp() const
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

void Sample::AddFrame(MyFrameInfo frame)
{
    _callstack.push_back(frame);
}

const std::vector<MyFrameInfo>& Sample::GetCallstack() const
{
    return _callstack;
}

std::string_view Sample::GetRuntimeId() const
{
    return _runtimeId;
}

const Labels& Sample::GetLabels() const
{
    return _allLabels;
}
