// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "EncodedPprof.h"
#include "Sample.h"
#include "Success.h"

#include <chrono>
#include <cstdint>
#include <map>
#include <memory>
#include <string>
#include <string_view>
#include <tuple>
#include <unordered_map>
#include <vector>

class IConfiguration;

// In-house replacement for libdatadog::Profile. Owns the pprof data model:
// string/function/location/mapping interning, sample aggregation, endpoint
// mapping/counts and upscaling rules. Serialize() produces the raw pprof
// protobuf bytes (no compression) via a single streaming pass.
//
// This type is NOT thread-safe: like the libdatadog Profile it replaces, a
// single instance is only ever mutated by the SamplesCollector worker thread.
class PprofBuilder
{
public:
    static std::unique_ptr<PprofBuilder> Create(
        IConfiguration* configuration,
        std::vector<SampleValueType> const& valueTypes,
        std::string const& periodType,
        std::string const& periodUnit,
        std::string applicationName);

    ~PprofBuilder() = default;

    PprofBuilder(PprofBuilder const&) = delete;
    PprofBuilder& operator=(PprofBuilder const&) = delete;

    libdatadog::Success Add(std::shared_ptr<Sample> const& sample);
    void SetEndpoint(int64_t traceId, std::string const& endpoint);
    void AddEndpointCount(std::string const& endpoint, int64_t count);
    libdatadog::Success AddUpscalingRuleProportional(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName, uint64_t sampled, uint64_t real);
    libdatadog::Success AddUpscalingRulePoisson(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName, uintptr_t sumValueOffset, uintptr_t countValueOffset, uint64_t samplingDistance);

    EncodedPprof Serialize();

    std::string const& GetApplicationName() const;

private:
    PprofBuilder(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, std::string applicationName, bool addTimestampOnSample);

    uint32_t InternString(std::string_view value);
    uint64_t InternFunction(uint32_t nameId, uint32_t systemNameId, uint32_t filenameId);
    uint64_t InternLocation(uint64_t functionId, int64_t line, uint64_t mappingId, uint64_t address);
    uint64_t InternMapping(uint32_t filenameId);

    struct PprofLabel
    {
        uint32_t Key = 0;
        bool IsStr = false;
        uint32_t Str = 0;   // string-table index (when IsStr)
        int64_t Num = 0;    // numeric value (when !IsStr)
        uint32_t NumUnit = 0;
    };

    struct PprofSample
    {
        std::vector<uint64_t> LocationIds;
        std::vector<PprofLabel> Labels;
        std::vector<int64_t> Values;
    };

    struct FunctionRecord
    {
        uint32_t Name;
        uint32_t SystemName;
        uint32_t Filename;
    };

    struct LocationRecord
    {
        uint64_t MappingId;
        uint64_t Address;
        uint64_t FunctionId;
        int64_t Line;
    };

    struct UpscalingRule
    {
        std::vector<size_t> Offsets;
        bool ApplyToAll = false;
        uint32_t LabelKeyId = 0;
        uint32_t LabelValueId = 0;
        bool IsPoisson = false;
        double ProportionalScale = 1.0;
        size_t SumOffset = 0;
        size_t CountOffset = 0;
        uint64_t SamplingDistance = 0;
    };

    static std::string MakeAggregationKey(std::vector<uint64_t> const& locationIds, std::vector<PprofLabel> const& labels);
    void ApplyUpscaling(std::vector<int64_t>& values, std::vector<PprofLabel> const& labels) const;
    void EnrichWithEndpoint(std::vector<PprofLabel>& labels) const;

    std::string _applicationName;
    bool _addTimestampOnSample;
    size_t _valuesCount;

    std::vector<SampleValueType> _valueTypes;
    std::string _periodType;
    std::string _periodUnit;

    std::chrono::system_clock::time_point _startTime;

    // Interning tables (index-stable within a profile cycle).
    std::vector<std::string> _stringTable;
    std::unordered_map<std::string, uint32_t> _stringToId;

    std::vector<FunctionRecord> _functions;
    std::map<std::tuple<uint32_t, uint32_t, uint32_t>, uint64_t> _functionIds;

    std::vector<LocationRecord> _locations;
    std::map<std::tuple<uint64_t, int64_t, uint64_t, uint64_t>, uint64_t> _locationIds;

    std::vector<uint32_t> _mappings; // filename string ids
    std::unordered_map<uint32_t, uint64_t> _mappingIds;

    // Samples: aggregated (non-timestamped) plus individually stored (timestamped).
    std::vector<PprofSample> _samples;
    std::unordered_map<std::string, size_t> _aggregationIndex;

    // Endpoints
    std::map<int64_t, uint32_t> _endpoints; // local root span id -> endpoint string id
    std::map<std::string, int64_t> _endpointCounts;

    std::vector<UpscalingRule> _upscalingRules;

    // Pre-interned well-known label keys.
    uint32_t _localRootSpanIdKeyId = 0;
    uint32_t _traceEndpointKeyId = 0;
    uint32_t _endTimestampKeyId = 0;
};
