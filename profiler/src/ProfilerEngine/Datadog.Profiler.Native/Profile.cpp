// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Profile.h"

#include "FfiHelper.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ProfileImpl.hpp"
#include "Sample.h"
#include "ScopeFinalizer.h"

#include <chrono>

namespace libdatadog {

using namespace std::chrono_literals;

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit);

std::unique_ptr<Profile> Profile::Create(IConfiguration* configuration, std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, std::string applicationName)
{
    auto impl = CreateProfile(valueTypes, periodType, periodUnit);
    if (impl == nullptr)
    {
        return nullptr;
    }
    return std::unique_ptr<Profile>(new Profile(std::move(impl), std::move(applicationName), configuration->IsTimestampsAsLabelEnabled()));
}

Profile::Profile(profile_unique_ptr impl, std::string applicationName, bool addTimestampOnSample) :
    _impl{std::move(impl)},
    _applicationName{std::move(applicationName)},
    _addTimestampOnSample{addTimestampOnSample}
{
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

        location = {};
        location.mapping_filename = to_poc_char_slice(frame.ModuleName);
        location.function.filename = to_poc_char_slice(frame.Filename);
        location.line = frame.StartLine; // For now we only have the start line of the function.
        location.function.name = to_poc_char_slice(frame.Frame);
        location.address = 0; // TODO check if we can get that information in the provider

        ++idx;
    }

    auto ffiSample = ddog_prof_sample{};
    ffiSample.locations = locations.data();
    ffiSample.locations_len = nbFrames;

    // Labels
    // PERF: since adding to a profile is done by only one thread (SamplesCollector worker thread),
    // we can reuse the same ffi labels vector for all samples.
    static std::vector<ddog_prof_label> ffiLabels;
    auto const& labels = sample->GetLabels();
    ffiLabels.reserve(labels.size());

    // PERF: clear the vector when the scope is left to avoid memory leaks.
    on_leave {
        ffiLabels.clear();
    };

    auto labelsVisitor = LabelsVisitor{
        [](NumericLabel const& l) -> ddog_prof_label {
            auto const& [name, value] = l;
            return ddog_prof_label {
                .key = {name.data(), name.size()},
                .num = value
            };
        },
        [](StringLabel const& l) -> ddog_prof_label {
            auto const& [name, value] = l;
            return ddog_prof_label {
                .key = {name.data(), name.size()},
                .str = {value.data(), value.size()}
            };
        }
    };

    for (auto const& label : labels)
    {
        auto ffiLabel = std::visit(labelsVisitor, label);
        ffiLabels.push_back(ffiLabel);
    }

    ffiSample.labels = ffiLabels.data();
    ffiSample.labels_len = ffiLabels.size();

    // values
    auto const& values = sample->GetValues();
    ffiSample.values = values.data();
    ffiSample.values_len = values.size();

    // add timestamp
    auto timestamp = 0ns;
    if (_addTimestampOnSample)
    {
        // All timestamps give the time when "something" ends and the associated duration
        // happened in the past
        timestamp = sample->GetTimeStamp();
    }

    auto add_res = ddog_prof_profile_add(profile, &ffiSample, timestamp.count());
    if (add_res != DDOG_OK)
    {
        return make_error(add_res);
    }
    return make_success();
}

void Profile::SetEndpoint(int64_t traceId, std::string const& endpoint)
{
    auto endpointName = to_poc_char_slice(endpoint);

    auto res = ddog_prof_profile_set_endpoint(*_impl, traceId, endpointName);
    if (res != DDOG_OK)
    {
        auto error = libdatadog::make_error(res);
        LogOnce(Info, "Unable to associate endpoint '", endpoint, "' to traced id '", traceId, "': ", error.message());
    }
}

void Profile::AddEndpointCount(std::string const& endpoint, int64_t count)
{
    auto endpointName = to_poc_char_slice(endpoint);

    auto res = ddog_prof_profile_add_endpoint_count(*_impl, endpointName, 1);
    if (res != DDOG_OK)
    {
        auto error = libdatadog::make_error(res);
        LogOnce(Info, "Unable to add count for endpoint '", endpoint, "': ", error.message());
    }
}

libdatadog::Success Profile::AddUpscalingRuleProportional(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName,
                                                          uint64_t sampled, uint64_t real)
{
    ddog_charslice labelName_slice = to_poc_char_slice(labelName);
    ddog_charslice groupName_slice = to_poc_char_slice(groupName);

    auto upscalingRuleAdd = ddog_prof_profile_add_upscaling_rule_proportional(*_impl, offsets.data(), offsets.size(), labelName_slice, groupName_slice, sampled, real);
    if (upscalingRuleAdd != DDOG_OK)
    {
        // not great, we create 2 Success
        // - the first one is to wrap the libdatadog error and ensure lifecycle is correctly handled
        // - the second one is to provide the caller with the actual error.
        // TODO: have a make_error(<format>, vars, ...) approach ?
        auto error = make_error(upscalingRuleAdd);
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
    ddog_charslice labelName_slice = to_poc_char_slice(labelName);
    ddog_charslice groupName_slice = to_poc_char_slice(groupName);

    auto upscalingRuleAdd = ddog_prof_profile_add_upscaling_rule_poisson(*_impl, offsets.data(), offsets.size(), labelName_slice, groupName_slice, sumValueOffset, countValueOffset, sampling_distance);
    if (upscalingRuleAdd != DDOG_OK)
    {
        auto error = make_error(upscalingRuleAdd);
        std::stringstream ss;
        ss << "(" << groupName << ", " << labelName << ") - [" << std::to_string(sumValueOffset) << ", " << std::to_string(countValueOffset) << ", " << std::to_string(sampling_distance) << "]:"
           << error.message();
        return make_error(ss.str());
    }

    return make_success();
}

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit)
{
    if (valueTypes.empty())
    {
        Log::Error("Cannot create profile: no sample value types defined. Ensure at least one profiler provider is enabled.");
        return nullptr;
    }

    std::vector<ddog_prof_value_type> samplesTypes;
    samplesTypes.reserve(valueTypes.size());

    for (auto const& type : valueTypes)
    {
        ddog_prof_value_type sampleType;
        if (!TryCreateSampleType(type.Name, type.Unit, sampleType))
        {
            Log::Error("Unsupported libdatadog sample type: ", type.Name, "/", type.Unit);
            return nullptr;
        }

        samplesTypes.push_back(sampleType);
    }

    ddog_prof_value_type periodSampleType;
    if (!TryCreateSampleType(periodType, periodUnit, periodSampleType))
    {
        Log::Error("Unsupported libdatadog period type: ", periodType, "/", periodUnit);
        return nullptr;
    }

    auto period = ddog_prof_period{};
    period.type = periodSampleType;
    period.value = 1;

    Log::Debug("Creating libdatadog profile with ", samplesTypes.size(), " sample type(s), ptr=", (void*)samplesTypes.data());

    ddog_prof_profile* rawProfile = nullptr;
    auto res = ddog_prof_profile_new(samplesTypes.data(), samplesTypes.size(), &period, &rawProfile);
    if (res != DDOG_OK)
    {
        auto error = libdatadog::make_error(res);
        Log::Error("ddog_prof_profile_new failed: ", error.message(),
                   " (sample_types count=", samplesTypes.size(),
                   ", period=", periodType, "/", periodUnit, ")");
        return nullptr;
    }
    return std::make_unique<ProfileImpl>(rawProfile);
}

std::string const& Profile::GetApplicationName() const
{
    return _applicationName;
}
} // namespace libdatadog
