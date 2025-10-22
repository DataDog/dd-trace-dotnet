// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include "IUpscaleProvider.h"

#include "Success.h"

#include <memory>
#include <string>
#include <system_error>
#include <vector>

class Sample;

struct SampleValueType;
class IConfiguration;

extern "C" {
    #include "datadog/profiling.h"
    #include "datadog/common.h"	
}

namespace libdatadog {

struct ProfileImpl;
class SymbolsStore;

class Profile
{
public:
    Profile(IConfiguration* configuration, std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, std::string applicationName, SymbolsStore* symbolsStore);
    ~Profile();

    Profile(Profile const&) = delete;
    Profile& operator=(Profile const&) = delete;

    Success Add(std::shared_ptr<Sample> const& sample);
    void SetEndpoint(int64_t traceId, std::string const& endpoint);
    void AddEndpointCount(std::string const& endpoint, int64_t count);
    Success AddUpscalingRuleProportional(std::uint64_t groupingIndex, ddog_prof_StringId labelName, std::string_view groupName, uint64_t sampled, uint64_t real);
    Success AddUpscalingRulePoisson(std::uint64_t groupingIndex, std::string_view labelName, std::string_view groupName, uintptr_t sumValueOffset, uintptr_t countValueOffset, uint64_t sampling_distance);
    std::string const& GetApplicationName() const;

private:
    friend class Exporter;
    std::unique_ptr<ProfileImpl> _impl;
    std::string _applicationName;
    bool _addTimestampOnSample;
    SymbolsStore* _symbolsStore;
};
} // namespace libdatadog
