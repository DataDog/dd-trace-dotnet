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
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

class AgentProxy
{
public:
    AgentProxy(ddog_prof_Exporter* exporter) :
        _exporter{exporter}
    {
    }

    ~AgentProxy() = default;

    Success Send(ddog_prof_EncodedProfile* profile, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata, std::string info)
    {
        auto [request, ec] = CreateRequest(profile, std::move(tags), std::move(files), std::move(metadata), std::move(info));
        if (!ec)
        {
            return std::move(ec); // ?? really ?? otherwise it calls the copy constructor :sad:
        }

        assert(request != nullptr);

        auto result = ddog_prof_Exporter_send(_exporter.get(), request, nullptr);

        if (result.tag == DDOG_PROF_EXPORTER_SEND_RESULT_ERR)
        {
            return make_error(result.err);
        }

        if (IsValidHttpCode(result.http_response.code))
        {
            return make_error(std::to_string(result.http_response.code));
        }

        return make_success();
    }

    bool IsValidHttpCode(int16_t code)
    {
        // Although we expect only 200, this range represents successful sends
        return code < 200 || code >= 300;
    }

private:
    struct Request
    {
        Request(ddog_prof_Exporter_Request* p) :
            _inner(p)
        {
        }
        Request(std::nullptr_t) :
            _inner(nullptr)
        {
        }

        ~Request()
        {
            ddog_prof_Exporter_Request_drop(&_inner);
        }

        Request(Request const&) = delete;
        Request& operator=(Request const&) = delete;

        Request(Request&& o) noexcept
        {
            *this = std::move(o);
        }

        Request& operator=(Request&& o) noexcept
        {
            if (this != &o)
            {
                _inner = std::exchange(o._inner, nullptr);
            }
            return *this;
        }

        operator ddog_prof_Exporter_Request**()
        {
            return &_inner;
        }

    private:
        ddog_prof_Exporter_Request* _inner;
    };

    std::pair<Request, Success> CreateRequest(ddog_prof_EncodedProfile* encodedProfile, Tags&& tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata, std::string info)
    {
        auto start = encodedProfile->start;
        auto end = encodedProfile->end;
        auto profileBuffer = encodedProfile->buffer;
        std::string const profile_filename = "auto.pprof";

        ddog_prof_Exporter_File profile{to_char_slice(profile_filename), ddog_Vec_U8_as_slice(&profileBuffer)};

        std::vector<ddog_prof_Exporter_File> uncompressed_files;
        uncompressed_files.reserve(1);
        // profile
        uncompressed_files.push_back(profile);

        std::vector<ddog_prof_Exporter_File> to_compress_files;
        to_compress_files.reserve(files.size());

        for (auto& [filename, content] : files)
        {
            ddog_Slice_U8 fileSlice{reinterpret_cast<const uint8_t*>(content.c_str()), content.size()};
            to_compress_files.push_back({to_char_slice(filename), fileSlice});
        }

        ddog_prof_Exporter_Slice_File uncompressed_files_view = {uncompressed_files.data(), uncompressed_files.size()};
        ddog_prof_Exporter_Slice_File to_compress_files_view = {to_compress_files.data(), to_compress_files.size()};

        ddog_CharSlice* pMetadata = nullptr;
        ddog_CharSlice ffi_metadata{};
        if (!metadata.empty())
        {
            ffi_metadata = to_char_slice(metadata);
            pMetadata = &ffi_metadata;
        }

        // json defined in internal RFC - Pprof System Info Support
        // that is used for SSI telemetry metrics.
        // Mostly already passed through tags today
        ddog_CharSlice* pInfo = nullptr;
        ddog_CharSlice ffi_info{};
        if (!info.empty())
        {
            ffi_info = to_char_slice(info);
            pInfo = &ffi_info;
        }

        auto* endpoints_stats = encodedProfile->endpoints_stats;
        auto requestResult =
            ddog_prof_Exporter_Request_build(
                _exporter.get(), start, end,
                to_compress_files_view, uncompressed_files_view,
                static_cast<ddog_Vec_Tag const*>(*tags._impl),
                endpoints_stats, pMetadata, pInfo);

        if (requestResult.tag == DDOG_PROF_EXPORTER_REQUEST_BUILD_RESULT_ERR)
        {
            return std::make_pair(Request{nullptr}, make_error(requestResult.err));
        }
        return std::make_pair(Request{requestResult.ok}, make_success());
    }

private:
    struct ExporterDeleter
    {
        void operator()(ddog_prof_Exporter* o)
        {
            ddog_prof_Exporter_drop(o);
        }
    };

    std::unique_ptr<ddog_prof_Exporter, ExporterDeleter> _exporter;
};
} // namespace libdatadog
