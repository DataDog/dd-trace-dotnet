// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ErrorCode.h"
#include "ErrorCodeImpl.hpp"
#include "FfiHelper.h"
#include "Tags.h"
#include "TagsImpl.hpp"

#include <cassert>
#include <memory>
#include <string>
#include <tuple>
#include <vector>

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog::detail {

class AgentExporter
{
public:
    AgentExporter(ddog_prof_Exporter* exporter) :
        _exporter{exporter}
    {
    }
    ~AgentExporter() = default;

    ErrorCode Send(ddog_prof_EncodedProfile* profile, Tags tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata)
    {
        auto [request, ec] = CreateRequest(profile, std::move(tags), std::move(files), std::move(metadata));
        if (!ec)
        {
            return std::move(ec); // ?? really ?? otherwise it calls the copy constructore :sad:
        }

        assert(request != nullptr);

        // TODO: should we use a cancellation token (third parameter) when shutting down takes to much time ?
        auto result = ddog_prof_Exporter_send(_exporter.get(), request, nullptr);

        if (result.tag == DDOG_PROF_EXPORTER_SEND_RESULT_ERR)
        {
            return detail::make_error(result.err);
        }

        if (IsValidHttpCode(result.http_response.code))
        {
            return detail::make_error(std::to_string(result.http_response.code));
        }

        return detail::make_success();
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

    std::pair<Request, ErrorCode> CreateRequest(ddog_prof_EncodedProfile* encodedProfile, Tags&& tags, std::vector<std::pair<std::string, std::string>> files, std::string metadata)
    {
        auto start = encodedProfile->start;
        auto end = encodedProfile->end;
        auto profileBuffer = encodedProfile->buffer;
        std::string const profile_filename = "auto.pprof";

        ddog_prof_Exporter_File profile{FfiHelper::StringToCharSlice(profile_filename), ddog_Vec_U8_as_slice(&profileBuffer)};

        std::vector<ddog_prof_Exporter_File> files_to_send;
        files_to_send.reserve(files.size() + 1);
        // profile
        files_to_send.push_back(profile);

        for (auto& [filename, content] : files)
        {
            ddog_Slice_U8 metricsFileSlice{reinterpret_cast<const uint8_t*>(content.c_str()), content.size()};
            files_to_send.push_back({FfiHelper::StringToCharSlice(filename), metricsFileSlice});
        }

        ddog_prof_Exporter_Slice_File files_view = {files_to_send.data(), files_to_send.size()};

        ddog_CharSlice* pMetadata = nullptr;
        ddog_CharSlice ffi_metadata{};

        if (!metadata.empty())
        {
            ffi_metadata = FfiHelper::StringToCharSlice(metadata);
            pMetadata = &ffi_metadata;
        }

        auto endpoints_stats = encodedProfile->endpoints_stats;
        auto requestResult = ddog_prof_Exporter_Request_build(_exporter.get(), start, end, files_view, static_cast<ddog_Vec_Tag const*>(*tags._impl), endpoints_stats, pMetadata, 10000);

        if (requestResult.tag == DDOG_PROF_EXPORTER_REQUEST_BUILD_RESULT_ERR)
        {
            return std::make_pair(Request{nullptr}, detail::make_error(requestResult.err));
        }
        return std::make_pair(Request{requestResult.ok}, detail::make_success());
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
} // namespace libdatadog::detail