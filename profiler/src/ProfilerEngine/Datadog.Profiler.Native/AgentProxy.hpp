// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "FfiHelper.h"
#include "Success.h"
#include "Tags.h"
#include "TagsImpl.hpp"

#include <cassert>
#include <memory>
#include <string>
#include <tuple>
#include <utility>
#include <vector>

extern "C"
{
#include "datadog_poc/common.h"
#include "datadog_poc/profiling.h"
}

namespace libdatadog {

class AgentProxy
{
public:
    AgentProxy(ddog_prof_exporter* exporter) :
        _exporter{exporter}
    {
    }

    ~AgentProxy()
    {
        ddog_prof_exporter_drop(_exporter);
    }

    Success Send(ddog_prof_encoded_profile* profile, Tags tags, std::vector<std::pair<std::string, std::vector<uint8_t>>> files, std::string metadata, std::string info, std::string processTags)
    {
        std::vector<ddog_prof_exporter_file> to_compress_files;
        to_compress_files.reserve(files.size());

        for (auto& [filename, content] : files)
        {
            ddog_byteslice fileSlice{content.data(), content.size()};
            to_compress_files.push_back({to_poc_char_slice(filename), fileSlice});
        }

        ddog_charslice* pMetadata = nullptr;
        ddog_charslice ffi_metadata{};
        if (!metadata.empty())
        {
            ffi_metadata = to_poc_char_slice(metadata);
            pMetadata = &ffi_metadata;
        }

        ddog_charslice* pInfo = nullptr;
        ddog_charslice ffi_info{};
        if (!info.empty())
        {
            ffi_info = to_poc_char_slice(info);
            pInfo = &ffi_info;
        }

        ddog_charslice* pProcessTags = nullptr;
        ddog_charslice ffi_processTags{};
        if (!processTags.empty())
        {
            ffi_processTags = to_poc_char_slice(processTags);
            pProcessTags = &ffi_processTags;
        }

        uint16_t httpStatus = 0;
        auto result =
            ddog_prof_exporter_send_blocking(
                _exporter,
                profile,
                to_compress_files.data(),
                to_compress_files.size(),
                static_cast<ddog_vec_tag const*>(*tags._impl),
                pProcessTags,
                pMetadata,
                pInfo,
                &httpStatus);

        if (result != DDOG_OK)
        {
            return make_error(result);
        }

        if (IsErrorHttpCode(httpStatus))
        {
            return make_error(std::to_string(httpStatus));
        }

        return make_success();
    }

    bool IsErrorHttpCode(uint16_t code)
    {
        // Although we expect only 200, we'll accept the whole range of valid codes
        return code < 200 || code >= 300;
    }

private:
    ddog_prof_exporter* _exporter;
};
} // namespace libdatadog
