// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Profile.h"

#include "FfiHelper.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ProfileImpl.hpp"
#include "Sample.h"
#include "ScopeFinalizer.h"
#include "SymbolsStore.h"

#include <chrono>

extern "C" {
    #include "datadog/profiling.h"
}

namespace libdatadog {

using namespace std::chrono_literals;

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, libdatadog::SymbolsStore* symbolsStore);

Profile::Profile(IConfiguration* configuration,
    std::vector<SampleValueType> const& valueTypes,
    std::string const& periodType,
    std::string const& periodUnit,
    std::string applicationName,
    libdatadog::SymbolsStore* pSymbolsStore) :
    _applicationName{std::move(applicationName)},
    _addTimestampOnSample{configuration->IsTimestampsAsLabelEnabled()},
    _pSymbolsStore{pSymbolsStore}
{
    _impl = CreateProfile(valueTypes, periodType, periodUnit, _pSymbolsStore);
}

Profile::~Profile() = default;

libdatadog::Success Profile::Add(std::shared_ptr<Sample> const& sample)
{
    auto const& callstack = sample->GetCallstack();
    auto nbFrames = callstack.size();

    auto& [locations, locationsSize, profile] = *_impl;

    if (nbFrames > locationsSize)
    {
        locationsSize = nbFrames;
        locations.resize(locationsSize);
    }

    std::size_t idx = 0UL;
    for (auto const& frame : callstack)
    {
        auto& location = locations[idx];

        location.mapping = reinterpret_cast<ddog_prof_MappingId2>(frame.ModuleId);
        location.function = reinterpret_cast<ddog_prof_FunctionId2>(frame.FunctionId);
        location.line = frame.StartLine; // For now we only have the start line of the function.

        ++idx;
    }

    auto ffiSample = ddog_prof_Sample2{};
    ffiSample.locations = {locations.data(), nbFrames};

    // Labels
    // PERF: since adding to a profile is done by only one thread (SamplesCollector worker thread),
    // we can reuse the same ffi labels vector for all samples.
    static std::vector<ddog_prof_Label2> ffiLabels;
    auto const& labels = sample->GetLabels();
    ffiLabels.reserve(labels.size());

    // PERF: clear the vector when the scope is left to avoid memory leaks.
    on_leave {
        ffiLabels.clear();
    };

    auto labelsVisitor = LabelsVisitor{
        [](NumericLabel const& l) -> ddog_prof_Label2 {
            auto const& [name, value] = l;
            return ddog_prof_Label2 {
                .key = reinterpret_cast<ddog_prof_StringId2>(name),
                .num = value
            };
        },
        [](StringLabel const& l) -> ddog_prof_Label2 {
            auto const& [name, value] = l;
            return ddog_prof_Label2 {
                .key = reinterpret_cast<ddog_prof_StringId2>(name),
                .str = {value.data(), value.size()}
            };
        }
    };

    for (auto const& label : labels)
    {
        auto ffiLabel = std::visit(labelsVisitor, label);
        ffiLabels.push_back(ffiLabel);
    }

    ffiSample.labels = {ffiLabels.data(), ffiLabels.size()};

    // values
    auto const& values = sample->GetValues();
    ffiSample.values = {values.data(), values.size()};

    // add timestamp
    auto timestamp = 0ns;
    if (_addTimestampOnSample)
    {
        // All timestamps give the time when "something" ends and the associated duration
        // happened in the past
        timestamp = sample->GetTimeStamp();
    }

    auto add_res = ddog_prof_Profile_add2(&profile, ffiSample, timestamp.count());
    if (add_res.err != nullptr)
    {
        return make_error(add_res.err);
    }
    return make_success();
}

void Profile::SetEndpoint(int64_t traceId, std::string const& endpoint)
{
    auto endpointName = to_char_slice(endpoint);

    auto res = ddog_prof_Profile_set_endpoint(*_impl, traceId, endpointName);
    if (res.tag == DDOG_PROF_PROFILE_RESULT_ERR)
    {
        // this is needed even though we already logged: to free the allocated error message
        auto error = libdatadog::make_error(res.err);
        LogOnce(Info, "Unable to associate endpoint '", endpoint, "' to traced id '", traceId, "': ", error.message());
    }
}

void Profile::AddEndpointCount(std::string const& endpoint, int64_t count)
{
    auto endpointName = to_char_slice(endpoint);

    auto res = ddog_prof_Profile_add_endpoint_count(*_impl, endpointName, 1);
    if (res.tag == DDOG_PROF_PROFILE_RESULT_ERR)
    {
        // this is needed even though we already logged: to free the allocated error message
        auto error = libdatadog::make_error(res.err);
        LogOnce(Info, "Unable to add count for endpoint '", endpoint, "': ", error.message());
    }
}

libdatadog::Success Profile::AddUpscalingRuleProportional(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName,
                                                          uint64_t sampled, uint64_t real)
{
    ddog_prof_Slice_Usize offsets_slice = {offsets.data(), offsets.size()};
    ddog_CharSlice labelName_slice = to_char_slice(labelName);
    ddog_CharSlice groupName_slice = to_char_slice(groupName);

    auto upscalingRuleAdd = ddog_prof_Profile_add_upscaling_rule_proportional(*_impl, offsets_slice, labelName_slice, groupName_slice, sampled, real);
    if (upscalingRuleAdd.tag == DDOG_PROF_PROFILE_RESULT_ERR)
    {
        // not great, we create 2 Success
        // - the first one is to wrap the libdatadog error and ensure lifecycle is correctly handled
        // - the second one is to provide the caller with the actual error.
        // TODO: have a make_error(<format>, vars, ...) approach ?
        auto error = make_error(upscalingRuleAdd.err);
        std::stringstream ss;
        ss << "(" << groupName << ", " << labelName << ") - [" << std::to_string(sampled) << "/" << std::to_string(real) << "]:"
           << error.message();
        return make_error(ss.str());
    }

    return make_success();
}

libdatadog::Success Profile::AddUpscalingRulePoisson(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName,
                                                          uintptr_t sumValueOffset, uintptr_t countValueOffset, uint64_t sampling_distance)
{
    ddog_prof_Slice_Usize offsets_slice = {offsets.data(), offsets.size()};
    ddog_CharSlice labelName_slice = to_char_slice(labelName);
    ddog_CharSlice groupName_slice = to_char_slice(groupName);

    auto upscalingRuleAdd = ddog_prof_Profile_add_upscaling_rule_poisson(*_impl, offsets_slice, labelName_slice, groupName_slice, sumValueOffset, countValueOffset, sampling_distance);
    if (upscalingRuleAdd.tag == DDOG_PROF_PROFILE_RESULT_ERR)
    {
        auto error = make_error(upscalingRuleAdd.err);
        std::stringstream ss;
        ss << "(" << groupName << ", " << labelName << ") - [" << std::to_string(sumValueOffset) << ", " << std::to_string(countValueOffset) << ", " << std::to_string(sampling_distance) << "]:"
           << error.message();
        return make_error(ss.str());
    }

    return make_success();
}

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, SymbolsStore* symbolsStore)
{
    std::vector<ddog_prof_ValueType> samplesTypes;
    samplesTypes.reserve(valueTypes.size());

    // TODO: create a vector<int32> containing the indexes of the valueTypes
    std::vector<int32_t> indexes;
    indexes.reserve(valueTypes.size());

    for (auto const& type : valueTypes)
    {
        samplesTypes.push_back(CreateValueType(type.Name, type.Unit));
        indexes.push_back(type.Index);
    }

    struct ddog_prof_Slice_ValueType sample_types = {samplesTypes.data(), samplesTypes.size()};

    auto period_value_type = CreateValueType(periodType, periodUnit);

    auto period = ddog_prof_Period{};
    period.type_ = period_value_type;
    period.value = 1;

    ddog_prof_Profile profile = {0};
    auto dictHandle = reinterpret_cast<ddog_prof_ProfilesDictionaryHandle>(symbolsStore->GetDictionary());
    auto res = ddog_prof_Profile_with_dictionary(&profile, &dictHandle, sample_types, &period);
    if (res.err != nullptr)
    {
        auto error = libdatadog::make_error(res);
        Log::Error("Failed to create profile with dictionary: ", error.message());
        return nullptr;
    }
    return std::make_unique<ProfileImpl>(profile);
}

std::string const& Profile::GetApplicationName() const
{
    return _applicationName;
}
} // namespace libdatadog
