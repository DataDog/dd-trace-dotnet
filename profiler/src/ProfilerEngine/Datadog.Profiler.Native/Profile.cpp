// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Profile.h"

#include "FfiHelper.h"
#include "Sample.h"
#include "ProfileImpl.hpp"
#include "ErrorCodeImpl.hpp"

namespace libdatadog {

libdatadog::detail::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit);

Profile::Profile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, std::string applicationName) :
    _impl{std::make_unique<detail::ProfileImpl>()},
    _applicationName{std::move(applicationName)}
{
    _impl->_lines.resize(_impl->_locationsAndLinesSize);
    _impl->_locations.resize(_impl->_locationsAndLinesSize);
    _impl->_inner = CreateProfile(valueTypes, periodType, periodUnit);
}

Profile::~Profile() = default;

libdatadog::ErrorCode Profile::Add(std::shared_ptr<Sample> const& sample)
{
    auto const& callstack = sample->GetCallstack();
    auto nbFrames = callstack.size();

    auto& [lines, locations, locationsAndLinesSize, profile] = *_impl;

    if (nbFrames > locationsAndLinesSize)
    {
        locationsAndLinesSize = nbFrames;
        locations.resize(locationsAndLinesSize);
        lines.resize(locationsAndLinesSize);
    }

    std::size_t idx = 0UL;
    for (auto const& frame : callstack)
    {
        auto& line = lines[idx];
        auto& location = locations[idx];

        line = {};
        line.function.filename = FfiHelper::StringToCharSlice(frame.Filename);
        line.function.start_line = frame.StartLine;
        line.function.name = FfiHelper::StringToCharSlice(frame.Frame);

        // add filename mapping
        location.mapping = {};
        location.mapping.filename = FfiHelper::StringToCharSlice(frame.ModuleName);
        location.address = 0; // TODO check if we can get that information in the provider
        location.lines = {&line, 1};
        location.is_folded = false;

        ++idx;
    }

    auto ffiSample = ddog_prof_Sample{};
    ffiSample.locations = {locations.data(), nbFrames};

    // Labels
    auto const& labels = sample->GetLabels();
    auto const& numericLabels = sample->GetNumericLabels();
    std::vector<ddog_prof_Label> ffiLabels;
    ffiLabels.reserve(labels.size() + numericLabels.size());

    for (auto const& [label, value] : labels)
    {
        ffiLabels.push_back({{label.data(), label.size()}, {value.data(), value.size()}});
    }

    for (auto const& [label, value] : numericLabels)
    {
        ffiLabels.push_back({{label.data(), label.size()}, {nullptr, 0}, value});
    }

    ffiSample.labels = {ffiLabels.data(), ffiLabels.size()};

    // values
    auto const& values = sample->GetValues();
    ffiSample.values = {values.data(), values.size()};

    auto add_res = ddog_prof_Profile_add(profile.get(), ffiSample);
    if (add_res.tag == DDOG_PROF_PROFILE_ADD_RESULT_ERR)
    {
        return detail::make_error(add_res.err);
    }
    return detail::make_success();
}

void Profile::SetEndpoint(int64_t traceId, std::string const& endpoint)
{
    auto endpointName = FfiHelper::StringToCharSlice(endpoint);

    ddog_prof_Profile_set_endpoint(_impl->_inner.get(), traceId, endpointName);
}

void Profile::AddEndpointCount(std::string const& endpoint, int64_t count)
{
    auto endpointName = FfiHelper::StringToCharSlice(endpoint);

    ddog_prof_Profile_add_endpoint_count(_impl->_inner.get(), endpointName, 1);
}

libdatadog::ErrorCode Profile::AddUpscalingRuleProportional(std::vector<std::uintptr_t> const& offsets, std::string_view labelName, std::string_view groupName,
                                                             uint64_t sampled, uint64_t real)
{
    ddog_prof_Slice_Usize offsets_slice = {offsets.data(), offsets.size()};
    ddog_CharSlice labelName_slice = FfiHelper::StringToCharSlice(labelName);
    ddog_CharSlice groupName_slice = FfiHelper::StringToCharSlice(groupName);

    auto upscalingRuleAdd = ddog_prof_Profile_add_upscaling_rule_proportional(*_impl, offsets_slice, labelName_slice, groupName_slice, sampled, real);
    if (upscalingRuleAdd.tag == DDOG_PROF_PROFILE_UPSCALING_RULE_ADD_RESULT_ERR)
    {
        // not great, we create 2 ErrorCode
        // - the first one is to wrap the libdatadog error and ensure lifecycle is correctly handled
        // - the second one is to provide the caller with the actual error.
        // TODO: have a make_error(<format>, vars, ...) approach ?
        auto error = detail::make_error(upscalingRuleAdd.err);
        std::stringstream ss;
        ss << "(" << groupName << ", " << labelName << ") - [" << std::to_string(sampled) << "/" << std::to_string(real) << "]:"
           << error.message();
        return detail::make_error(ss.str());
    }

    return detail::make_success();
}

libdatadog::detail::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit)
{
    std::vector<ddog_prof_ValueType> samplesTypes;
    samplesTypes.reserve(valueTypes.size());

    for (auto const& type : valueTypes)
    {
        samplesTypes.push_back(FfiHelper::CreateValueType(type.Name, type.Unit));
    }

    struct ddog_prof_Slice_ValueType sample_types = {samplesTypes.data(), samplesTypes.size()};

    auto period_value_type = FfiHelper::CreateValueType(periodType, periodUnit);

    auto period = ddog_prof_Period{};
    period.type_ = period_value_type;
    period.value = 1;

    return detail::profile_unique_ptr(ddog_prof_Profile_new(sample_types, &period, nullptr));
}

std::string const& Profile::GetApplicationName() const
{
    return _applicationName;
}
} // namespace libdatadog
