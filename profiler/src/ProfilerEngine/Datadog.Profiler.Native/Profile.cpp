// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "Profile.h"

#include "FfiHelper.h"
#include "IConfiguration.h"
#include "Log.h"
#include "ProfileImpl.hpp"
#include "Sample.h"
#include "StringId.hpp"
#include <chrono>

namespace libdatadog {

using namespace std::chrono_literals;

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit);

Profile::Profile(IConfiguration* configuration, std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit, std::string applicationName) :
    _applicationName{std::move(applicationName)},
    _addTimestampOnSample{configuration->IsTimestampsAsLabelEnabled()}
{
    _impl = CreateProfile(valueTypes, periodType, periodUnit);
}

Profile::~Profile() = default;

std::pair<ddog_prof_StringId, Success> InternString(ddog_prof_Profile* profile, std::string_view s)
{
    auto result = ddog_prof_Profile_intern_string(profile, to_char_slice(s));
    if (result.tag == DDOG_PROF_STRING_ID_RESULT_ERR_GENERATIONAL_ID_STRING_ID)
    {
        return {ddog_prof_StringId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

std::pair<ddog_prof_MappingId, Success> InternMapping(ddog_prof_Profile* profile, std::string_view moduleName)
{
    auto [id, error] = InternString(profile, moduleName);

    if (!error)
    {
        return {ddog_prof_MappingId{}, std::move(error)};
    }

    auto result = ddog_prof_Profile_intern_mapping(profile, 0, 0, 0, id, ddog_prof_Profile_interned_empty_string());

    if (result.tag == DDOG_PROF_MAPPING_ID_RESULT_ERR_GENERATIONAL_ID_MAPPING_ID)
    {
        return {ddog_prof_MappingId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

std::pair<ddog_prof_FunctionId, Success> InternFunction(
    ddog_prof_Profile* profile, std::string_view fileName, InternedStringView frame)
{
    auto [filenameId, error] = InternString(profile, fileName);

    if (!error)
    {
        return {ddog_prof_FunctionId{}, std::move(error)};
    }

    auto profileId = ddog_prof_Profile_get_generation(profile);
    if (frame._impl == nullptr || !ddog_prof_Profile_generations_are_equal(profileId.ok, frame._impl->Id.generation))
    {
        auto [sid, error] = InternString(profile, frame._s);
        if (!error)
        {
            return {ddog_prof_FunctionId{}, std::move(error)};
        }
        if (frame._impl == nullptr)
        {
            frame._impl = std::make_shared<StringId>();
        }
        frame._impl->Id = sid;
    }

    auto result = ddog_prof_Profile_intern_function(profile, frame._impl->Id, ddog_prof_Profile_interned_empty_string(), filenameId);
    
    if (result.tag == DDOG_PROF_FUNCTION_ID_RESULT_ERR_GENERATIONAL_ID_FUNCTION_ID)
    {
        return {ddog_prof_FunctionId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

std::pair<ddog_prof_StackTraceId, Success> InternStacktrace(ddog_prof_Profile* profile, ddog_prof_Slice_LocationId locations)
{
    auto result = ddog_prof_Profile_intern_stacktrace(profile, locations);
    if (result.tag == DDOG_PROF_STACK_TRACE_ID_RESULT_ERR_GENERATIONAL_ID_STACK_TRACE_ID)
    {
        return {ddog_prof_StackTraceId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

std::pair<ddog_prof_LocationId, Success> InternLocation(
    ddog_prof_Profile* profile, ddog_prof_MappingId mapping, ddog_prof_FunctionId function, int64_t line)
{
    auto result = ddog_prof_Profile_intern_location_with_mapping_id(profile, mapping, function, 0, line);
    if (result.tag == DDOG_PROF_LOCATION_ID_RESULT_ERR_GENERATIONAL_ID_LOCATION_ID)
    {
        return {ddog_prof_LocationId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

std::pair<ddog_prof_LabelId, Success> InternStringLabel(
    ddog_prof_Profile* profile, std::string_view name, std::string_view value
)
{
    std::array<ddog_CharSlice, 2> strings = {to_char_slice(name), to_char_slice(value)};
    std::array<ddog_prof_StringId, 2> stringIds;
    ddog_prof_MutSlice_GenerationalIdStringId slice = {.ptr = stringIds.data(), .len = 2};

    auto v = ddog_prof_Profile_intern_strings(profile, {strings.data(), 2}, slice);
    if (v.tag == DDOG_VOID_RESULT_ERR)
    {
        return {ddog_prof_LabelId{}, make_error(v.err)};
    }

    auto result = ddog_prof_Profile_intern_label_str(profile, stringIds[0], stringIds[1]);
    if (result.tag == DDOG_PROF_LABEL_ID_RESULT_ERR_GENERATIONAL_ID_LABEL_ID)
    {
        return {ddog_prof_LabelId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

std::pair<ddog_prof_LabelId, Success> InternNumericLabel(
    ddog_prof_Profile* profile, std::string_view name, int64_t value
)
{
    auto [nameId, success] = InternString(profile, name);
    if (!success)
    {
        return {ddog_prof_LabelId{}, std::move(success)};
    }

    auto result = ddog_prof_Profile_intern_label_num(profile, nameId, value);
    if (result.tag == DDOG_PROF_LABEL_ID_RESULT_ERR_GENERATIONAL_ID_LABEL_ID)
    {
        return {ddog_prof_LabelId{}, make_error(result.err)};
    }

    return {result.ok, make_success()};
}

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

        auto [mapping, success] = InternMapping(&profile, frame.ModuleName);
        if (!success)
        {
            return std::move(success);
        }

        auto [function, success2] = InternFunction(&profile, frame.Filename, frame.Frame);
        if (!success2)
        {
            return std::move(success2);
        }

        std::tie(location, success) = InternLocation(&profile, mapping, function, frame.StartLine);
        if (!success)
        {
            return std::move(success);
        }

        ++idx;
    }

    auto [stackTrace, success] = InternStacktrace(&profile, {locations.data(), nbFrames});
    if (!success)
    {
        return std::move(success);
    }

    // Labels
    auto const& labels = sample->GetLabels();
    auto const& numericLabels = sample->GetNumericLabels();
    std::vector<ddog_prof_LabelId> ffiLabels;
    ffiLabels.reserve(labels.size() + numericLabels.size());

    for (auto const& [label, value] : labels)
    {
        auto [labelz, success] = InternStringLabel(&profile, label, value);
        if (!success)
        {
            // skip ?? or stop
            continue;
        }
        ffiLabels.push_back(std::move(labelz));
    }

    for (auto const& [label, value] : numericLabels)
    {
        auto [labelz, success] = InternNumericLabel(&profile, label, value);
        if (!success)
        {
            // skip ?? or stop
            continue;
        }
        ffiLabels.push_back(std::move(labelz));
    }

    auto values = sample->GetValues();
    // add timestamp
    auto timestamp = 0ns;
    if (_addTimestampOnSample)
    {
        // All timestamps give the time when "something" ends and the associated duration
        // happened in the past
        timestamp = sample->GetTimeStamp();
    }

    auto ss = ddog_prof_Profile_intern_labelset(&profile, {ffiLabels.data(), ffiLabels.size()});

    auto rr = ddog_prof_Profile_intern_sample(&profile, stackTrace,
        {values.data(), values.size()},
        ss.ok,
        timestamp.count());

    if (rr.tag == DDOG_VOID_RESULT_ERR)
    {
        return make_error(rr.err);
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

libdatadog::profile_unique_ptr CreateProfile(std::vector<SampleValueType> const& valueTypes, std::string const& periodType, std::string const& periodUnit)
{
    std::vector<ddog_prof_ValueType> samplesTypes;
    samplesTypes.reserve(valueTypes.size());

    for (auto const& type : valueTypes)
    {
        samplesTypes.push_back(CreateValueType(type.Name, type.Unit));
    }

    struct ddog_prof_Slice_ValueType sample_types = {samplesTypes.data(), samplesTypes.size()};

    auto period_value_type = CreateValueType(periodType, periodUnit);

    auto period = ddog_prof_Period{};
    period.type_ = period_value_type;
    period.value = 1;

    auto res = ddog_prof_Profile_new(sample_types, &period);
    if (res.tag == DDOG_PROF_PROFILE_NEW_RESULT_ERR)
    {
        return nullptr;
    }
    return std::make_unique<ProfileImpl>(res.ok);
}

std::string const& Profile::GetApplicationName() const
{
    return _applicationName;
}
} // namespace libdatadog
