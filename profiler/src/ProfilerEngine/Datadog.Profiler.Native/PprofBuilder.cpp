// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "PprofBuilder.h"

#include "FfiHelper.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ProtobufWriter.h"

#include <cmath>
#include <cstring>

namespace {
// pprof field numbers (see profiler/test/.../Helpers/Pprof/pprof.proto)
namespace ProfileField {
constexpr uint32_t SampleType = 1;
constexpr uint32_t Sample = 2;
constexpr uint32_t Mapping = 3;
constexpr uint32_t Location = 4;
constexpr uint32_t Function = 5;
constexpr uint32_t StringTable = 6;
constexpr uint32_t TimeNanos = 9;
constexpr uint32_t DurationNanos = 10;
constexpr uint32_t PeriodType = 11;
constexpr uint32_t Period = 12;
} // namespace ProfileField

namespace SampleField {
constexpr uint32_t LocationId = 1;
constexpr uint32_t Value = 2;
constexpr uint32_t Label = 3;
} // namespace SampleField

namespace LabelField {
constexpr uint32_t Key = 1;
constexpr uint32_t Str = 2;
constexpr uint32_t Num = 3;
constexpr uint32_t NumUnit = 4;
} // namespace LabelField

namespace ValueTypeField {
constexpr uint32_t Type = 1;
constexpr uint32_t Unit = 2;
} // namespace ValueTypeField

namespace MappingField {
constexpr uint32_t Id = 1;
constexpr uint32_t Filename = 5;
} // namespace MappingField

namespace LocationField {
constexpr uint32_t Id = 1;
constexpr uint32_t MappingId = 2;
constexpr uint32_t Address = 3;
constexpr uint32_t Line = 4;
} // namespace LocationField

namespace LineField {
constexpr uint32_t FunctionId = 1;
constexpr uint32_t Line = 2;
} // namespace LineField

namespace FunctionField {
constexpr uint32_t Id = 1;
constexpr uint32_t Name = 2;
constexpr uint32_t SystemName = 3;
constexpr uint32_t Filename = 4;
} // namespace FunctionField

constexpr const char* TraceEndpointLabel = "trace endpoint";
constexpr const char* EndTimestampLabel = "end_timestamp_ns";

void AppendUint64(std::string& key, uint64_t value)
{
    char buffer[sizeof(uint64_t)];
    std::memcpy(buffer, &value, sizeof(value));
    key.append(buffer, sizeof(buffer));
}
} // namespace

std::unique_ptr<PprofBuilder> PprofBuilder::Create(
    IConfiguration* configuration,
    std::vector<SampleValueType> const& valueTypes,
    std::string const& periodType,
    std::string const& periodUnit,
    std::string applicationName)
{
    if (valueTypes.empty())
    {
        Log::Error("Cannot create profile: no sample value types defined. Ensure at least one profiler provider is enabled.");
        return nullptr;
    }

    return std::unique_ptr<PprofBuilder>(
        new PprofBuilder(valueTypes, periodType, periodUnit, std::move(applicationName), configuration->IsTimestampsAsLabelEnabled()));
}

PprofBuilder::PprofBuilder(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, std::string applicationName, bool addTimestampOnSample) :
    _applicationName{std::move(applicationName)},
    _addTimestampOnSample{addTimestampOnSample},
    _valuesCount{valueTypes.size()},
    _valueTypes{valueTypes},
    _periodType{periodType},
    _periodUnit{periodUnit},
    _startTime{std::chrono::system_clock::now()}
{
    // string_table[0] must always be the empty string.
    InternString("");

    // Pre-intern the well-known label keys (matches libdatadog's try_new_internal).
    _localRootSpanIdKeyId = InternString(Sample::LocalRootSpanIdLabel);
    _traceEndpointKeyId = InternString(TraceEndpointLabel);
    _endTimestampKeyId = InternString(EndTimestampLabel);

    // Pre-intern the sample-type and period strings so the string table is fully
    // populated with them before Serialize() writes it (Serialize only ever looks
    // these ids back up, it never adds new strings).
    for (auto const& valueType : _valueTypes)
    {
        InternString(valueType.Name);
        InternString(valueType.Unit);
    }
    InternString(_periodType);
    InternString(_periodUnit);
}

std::string const& PprofBuilder::GetApplicationName() const
{
    return _applicationName;
}

uint32_t PprofBuilder::InternString(std::string_view value)
{
    auto it = _stringToId.find(std::string(value));
    if (it != _stringToId.end())
    {
        return it->second;
    }

    auto id = static_cast<uint32_t>(_stringTable.size());
    _stringTable.emplace_back(value);
    _stringToId.emplace(_stringTable.back(), id);
    return id;
}

uint64_t PprofBuilder::InternFunction(uint32_t nameId, uint32_t systemNameId, uint32_t filenameId)
{
    auto key = std::make_tuple(nameId, systemNameId, filenameId);
    auto it = _functionIds.find(key);
    if (it != _functionIds.end())
    {
        return it->second;
    }

    _functions.push_back(FunctionRecord{nameId, systemNameId, filenameId});
    auto id = static_cast<uint64_t>(_functions.size()); // 1-based
    _functionIds.emplace(key, id);
    return id;
}

uint64_t PprofBuilder::InternMapping(uint32_t filenameId)
{
    auto it = _mappingIds.find(filenameId);
    if (it != _mappingIds.end())
    {
        return it->second;
    }

    _mappings.push_back(filenameId);
    auto id = static_cast<uint64_t>(_mappings.size()); // 1-based
    _mappingIds.emplace(filenameId, id);
    return id;
}

uint64_t PprofBuilder::InternLocation(uint64_t functionId, int64_t line, uint64_t mappingId, uint64_t address)
{
    auto key = std::make_tuple(functionId, line, mappingId, address);
    auto it = _locationIds.find(key);
    if (it != _locationIds.end())
    {
        return it->second;
    }

    _locations.push_back(LocationRecord{mappingId, address, functionId, line});
    auto id = static_cast<uint64_t>(_locations.size()); // 1-based
    _locationIds.emplace(key, id);
    return id;
}

std::string PprofBuilder::MakeAggregationKey(std::vector<uint64_t> const& locationIds, std::vector<PprofLabel> const& labels)
{
    std::string key;
    key.reserve((locationIds.size() + labels.size() * 4 + 1) * sizeof(uint64_t));

    for (auto id : locationIds)
    {
        AppendUint64(key, id);
    }

    key.push_back('\x1f'); // separator between locations and labels

    for (auto const& label : labels)
    {
        AppendUint64(key, label.Key);
        AppendUint64(key, label.IsStr ? 1u : 0u);
        AppendUint64(key, label.IsStr ? label.Str : static_cast<uint64_t>(label.Num));
        AppendUint64(key, label.NumUnit);
    }

    return key;
}

libdatadog::Success PprofBuilder::Add(std::shared_ptr<Sample> const& sample)
{
    auto const& callstack = sample->GetCallstack();

    std::vector<uint64_t> locationIds;
    locationIds.reserve(callstack.size());

    for (auto const& frame : callstack)
    {
        auto nameId = InternString(frame.Frame);
        auto filenameId = InternString(frame.Filename);
        auto functionId = InternFunction(nameId, 0, filenameId);

        auto moduleId = InternString(frame.ModuleName);
        auto mappingId = InternMapping(moduleId);

        auto locationId = InternLocation(functionId, static_cast<int64_t>(frame.StartLine), mappingId, 0);
        locationIds.push_back(locationId);
    }

    std::vector<PprofLabel> labels;
    auto const& sampleLabels = sample->GetLabels();
    labels.reserve(sampleLabels.size() + 1);

    auto labelsVisitor = LabelsVisitor{
        [this, &labels](NumericLabel const& l) {
            auto const& [name, value] = l;
            labels.push_back(PprofLabel{InternString(name), false, 0, value, 0});
        },
        [this, &labels](StringLabel const& l) {
            auto const& [name, value] = l;
            labels.push_back(PprofLabel{InternString(name), true, InternString(value), 0, 0});
        }};

    for (auto const& label : sampleLabels)
    {
        std::visit(labelsVisitor, label);
    }

    std::vector<int64_t> values(_valuesCount, 0);
    auto const& sampleValues = sample->GetValues();
    auto count = std::min(_valuesCount, sampleValues.size());
    for (size_t i = 0; i < count; ++i)
    {
        values[i] = sampleValues[i];
    }

    auto timestamp = std::chrono::nanoseconds(0);
    if (_addTimestampOnSample)
    {
        timestamp = sample->GetTimeStamp();
    }

    if (timestamp.count() != 0)
    {
        // Timestamped samples are emitted individually (never aggregated) with an
        // end_timestamp_ns numeric label carrying the timestamp.
        labels.push_back(PprofLabel{_endTimestampKeyId, false, 0, timestamp.count(), 0});
        _samples.push_back(PprofSample{std::move(locationIds), std::move(labels), std::move(values)});
        return libdatadog::make_success();
    }

    auto key = MakeAggregationKey(locationIds, labels);
    auto it = _aggregationIndex.find(key);
    if (it != _aggregationIndex.end())
    {
        auto& aggregated = _samples[it->second].Values;
        for (size_t i = 0; i < _valuesCount; ++i)
        {
            aggregated[i] += values[i];
        }
        return libdatadog::make_success();
    }

    auto index = _samples.size();
    _samples.push_back(PprofSample{std::move(locationIds), std::move(labels), std::move(values)});
    _aggregationIndex.emplace(std::move(key), index);
    return libdatadog::make_success();
}

void PprofBuilder::SetEndpoint(int64_t traceId, std::string const& endpoint)
{
    _endpoints[traceId] = InternString(endpoint);
}

void PprofBuilder::AddEndpointCount(std::string const& endpoint, int64_t count)
{
    _endpointCounts[endpoint] += count;
}

libdatadog::Success PprofBuilder::AddUpscalingRuleProportional(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName, uint64_t sampled, uint64_t real)
{
    if (sampled == 0)
    {
        std::stringstream ss;
        ss << "(" << groupName << ", " << labelName << ") - [" << sampled << "/" << real << "]: sampled count cannot be 0";
        return libdatadog::make_error(ss.str());
    }

    UpscalingRule rule;
    rule.Offsets.reserve(offsets.size());
    for (auto offset : offsets)
    {
        rule.Offsets.push_back(static_cast<size_t>(offset));
    }
    rule.ApplyToAll = labelName.empty();
    rule.LabelKeyId = labelName.empty() ? 0 : InternString(labelName);
    rule.LabelValueId = groupName.empty() ? 0 : InternString(groupName);
    rule.IsPoisson = false;
    rule.ProportionalScale = static_cast<double>(real) / static_cast<double>(sampled);

    _upscalingRules.push_back(std::move(rule));
    return libdatadog::make_success();
}

libdatadog::Success PprofBuilder::AddUpscalingRulePoisson(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName, uintptr_t sumValueOffset, uintptr_t countValueOffset, uint64_t samplingDistance)
{
    if (samplingDistance == 0)
    {
        std::stringstream ss;
        ss << "(" << groupName << ", " << labelName << ") - [" << sumValueOffset << ", " << countValueOffset << ", " << samplingDistance << "]: sampling distance cannot be 0";
        return libdatadog::make_error(ss.str());
    }

    UpscalingRule rule;
    rule.Offsets.reserve(offsets.size());
    for (auto offset : offsets)
    {
        rule.Offsets.push_back(static_cast<size_t>(offset));
    }
    rule.ApplyToAll = labelName.empty();
    rule.LabelKeyId = labelName.empty() ? 0 : InternString(labelName);
    rule.LabelValueId = groupName.empty() ? 0 : InternString(groupName);
    rule.IsPoisson = true;
    rule.SumOffset = static_cast<size_t>(sumValueOffset);
    rule.CountOffset = static_cast<size_t>(countValueOffset);
    rule.SamplingDistance = samplingDistance;

    _upscalingRules.push_back(std::move(rule));
    return libdatadog::make_success();
}

void PprofBuilder::EnrichWithEndpoint(std::vector<PprofLabel>& labels) const
{
    if (_endpoints.empty())
    {
        return;
    }

    for (auto const& label : labels)
    {
        if (label.IsStr || label.Key != _localRootSpanIdKeyId)
        {
            continue;
        }

        auto it = _endpoints.find(label.Num);
        if (it != _endpoints.end())
        {
            labels.push_back(PprofLabel{_traceEndpointKeyId, true, it->second, 0, 0});
        }
        break;
    }
}

void PprofBuilder::ApplyUpscaling(std::vector<int64_t>& values, std::vector<PprofLabel> const& labels) const
{
    for (auto const& rule : _upscalingRules)
    {
        bool matches = rule.ApplyToAll;
        if (!matches)
        {
            for (auto const& label : labels)
            {
                if (label.IsStr && label.Key == rule.LabelKeyId && label.Str == rule.LabelValueId)
                {
                    matches = true;
                    break;
                }
            }
        }

        if (!matches)
        {
            continue;
        }

        double scale = rule.ProportionalScale;
        if (rule.IsPoisson)
        {
            scale = 1.0;
            if (rule.SumOffset < values.size() && rule.CountOffset < values.size())
            {
                auto sum = values[rule.SumOffset];
                auto sampledCount = values[rule.CountOffset];
                if (sum != 0 && sampledCount != 0)
                {
                    double average = static_cast<double>(sum) / static_cast<double>(sampledCount);
                    scale = 1.0 / (1.0 - std::exp(-average / static_cast<double>(rule.SamplingDistance)));
                }
            }
        }

        for (auto offset : rule.Offsets)
        {
            if (offset < values.size())
            {
                values[offset] = static_cast<int64_t>(std::llround(static_cast<double>(values[offset]) * scale));
            }
        }
    }
}

EncodedPprof PprofBuilder::Serialize()
{
    EncodedPprof result;
    result.Start = _startTime;
    result.End = std::chrono::system_clock::now();
    result.EndpointCounts = _endpointCounts;

    auto& out = result.Bytes;
    out.clear();

    // Scratch buffers reused across the whole serialization pass to avoid churn.
    std::vector<uint8_t> sampleMsg;
    std::vector<uint8_t> labelMsg;
    std::vector<uint8_t> subMsg;
    std::vector<uint8_t> packedScratch;
    std::vector<int64_t> scaledValues;
    std::vector<PprofLabel> enrichedLabels;

    // 1. Samples (field 2)
    for (auto const& sample : _samples)
    {
        sampleMsg.clear();

        protobuf::WritePackedVarints(sampleMsg, packedScratch, SampleField::LocationId,
                                     sample.LocationIds.data(), sample.LocationIds.size());

        scaledValues = sample.Values;
        enrichedLabels = sample.Labels;
        EnrichWithEndpoint(enrichedLabels);
        ApplyUpscaling(scaledValues, enrichedLabels);

        protobuf::WritePackedInt64(sampleMsg, packedScratch, SampleField::Value,
                                   scaledValues.data(), scaledValues.size());

        for (auto const& label : enrichedLabels)
        {
            labelMsg.clear();
            protobuf::WriteVarintField(labelMsg, LabelField::Key, label.Key);
            if (label.IsStr)
            {
                protobuf::WriteVarintField(labelMsg, LabelField::Str, label.Str);
            }
            else
            {
                protobuf::WriteInt64Field(labelMsg, LabelField::Num, label.Num);
                if (label.NumUnit != 0)
                {
                    protobuf::WriteVarintField(labelMsg, LabelField::NumUnit, label.NumUnit);
                }
            }
            protobuf::WriteMessageField(sampleMsg, SampleField::Label, labelMsg);
        }

        protobuf::WriteMessageField(out, ProfileField::Sample, sampleMsg);
    }

    // 2. Sample types (field 1)
    for (auto const& valueType : _valueTypes)
    {
        subMsg.clear();
        protobuf::WriteVarintField(subMsg, ValueTypeField::Type, InternString(valueType.Name));
        protobuf::WriteVarintField(subMsg, ValueTypeField::Unit, InternString(valueType.Unit));
        protobuf::WriteMessageField(out, ProfileField::SampleType, subMsg);
    }

    // 3. Mappings (field 3)
    for (size_t i = 0; i < _mappings.size(); ++i)
    {
        subMsg.clear();
        protobuf::WriteVarintField(subMsg, MappingField::Id, static_cast<uint64_t>(i + 1));
        protobuf::WriteVarintField(subMsg, MappingField::Filename, _mappings[i]);
        protobuf::WriteMessageField(out, ProfileField::Mapping, subMsg);
    }

    // 4. Locations (field 4)
    for (size_t i = 0; i < _locations.size(); ++i)
    {
        auto const& location = _locations[i];
        subMsg.clear();
        protobuf::WriteVarintField(subMsg, LocationField::Id, static_cast<uint64_t>(i + 1));
        if (location.MappingId != 0)
        {
            protobuf::WriteVarintField(subMsg, LocationField::MappingId, location.MappingId);
        }
        if (location.Address != 0)
        {
            protobuf::WriteVarintField(subMsg, LocationField::Address, location.Address);
        }

        labelMsg.clear(); // reuse as the Line sub-message buffer
        protobuf::WriteVarintField(labelMsg, LineField::FunctionId, location.FunctionId);
        protobuf::WriteInt64Field(labelMsg, LineField::Line, location.Line);
        protobuf::WriteMessageField(subMsg, LocationField::Line, labelMsg);

        protobuf::WriteMessageField(out, ProfileField::Location, subMsg);
    }

    // 5. Functions (field 5)
    for (size_t i = 0; i < _functions.size(); ++i)
    {
        auto const& function = _functions[i];
        subMsg.clear();
        protobuf::WriteVarintField(subMsg, FunctionField::Id, static_cast<uint64_t>(i + 1));
        protobuf::WriteVarintField(subMsg, FunctionField::Name, function.Name);
        if (function.SystemName != 0)
        {
            protobuf::WriteVarintField(subMsg, FunctionField::SystemName, function.SystemName);
        }
        protobuf::WriteVarintField(subMsg, FunctionField::Filename, function.Filename);
        protobuf::WriteMessageField(out, ProfileField::Function, subMsg);
    }

    // 6. String table (field 6)
    for (auto const& str : _stringTable)
    {
        protobuf::WriteStringField(out, ProfileField::StringTable, str);
    }

    // 9/10. Collection window
    auto startNs = std::chrono::duration_cast<std::chrono::nanoseconds>(_startTime.time_since_epoch()).count();
    auto endNs = std::chrono::duration_cast<std::chrono::nanoseconds>(result.End.time_since_epoch()).count();
    protobuf::WriteInt64Field(out, ProfileField::TimeNanos, startNs);
    protobuf::WriteInt64Field(out, ProfileField::DurationNanos, endNs - startNs);

    // 11. Period type
    subMsg.clear();
    protobuf::WriteVarintField(subMsg, ValueTypeField::Type, InternString(_periodType));
    protobuf::WriteVarintField(subMsg, ValueTypeField::Unit, InternString(_periodUnit));
    protobuf::WriteMessageField(out, ProfileField::PeriodType, subMsg);

    // 12. Period
    protobuf::WriteInt64Field(out, ProfileField::Period, 1);

    return result;
}
