// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <fstream>
#include <string>

#include "EncodedProfile.hpp"
#include "FfiHelper.h"
#include "FileHelper.h"
#include "OpSysTools.h"
#include "Success.h"

#include "shared/src/native-src/dd_filesystem.hpp"

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

namespace libdatadog {

class FileSaver
{
public:
    FileSaver(fs::path outputDirectory) :
        _outputDirectory{outputDirectory}
    {
    }

    ~FileSaver() = default;

    Success WriteToDisk(EncodedProfile& profile, std::string const& serviceName, std::vector<std::pair<std::string, std::string>> const& files, std::string const& metadata, std::string const& info)
    {
        auto const& profileId = profile.GetId();
        auto success = WriteProfileToDisk(profile, serviceName, profileId);

        bool hasError = false;

        std::stringstream errorMessage;
        if (!success)
        {
            hasError = true;
            errorMessage << success.message() << "\n";
        }

        for (auto const& [filename, content] : files)
        {
            success = WriteTextFileToDisk(filename, content, serviceName, profileId);

            if (!success)
            {
                errorMessage << success.message() << "\n";
                hasError = true;
            }
        }

        if (!metadata.empty())
        {
            static const std::string MetadataFilename = "metadata.json";
            success = WriteTextFileToDisk(MetadataFilename, metadata, serviceName, profileId);

            if (!success)
            {
                errorMessage << success.message() << "\n";
                hasError = true;
            }
        }

        if (!info.empty())
        {
            static const std::string InfoFilename = "info.json";
            success = WriteTextFileToDisk(InfoFilename, info, serviceName, profileId);

            if (!success)
            {
                errorMessage << success.message() << "\n";
                hasError = true;
            }
        }

        if (hasError)
        {
            return make_error(errorMessage.str());
        }
        return make_success();
    }

private:
    Success WriteProfileToDisk(ddog_prof_EncodedProfile* profile, std::string const& serviceName, std::string const& uid)
    {
        // no specific filename for the pprof file
        auto filepath = GenerateFilePath("", ".pprof", serviceName, uid);
        auto resultBytes = ddog_prof_EncodedProfile_bytes(profile);

        if (resultBytes.tag == DDOG_PROF_RESULT_BYTE_SLICE_ERR_BYTE_SLICE)
        {
            return make_error(resultBytes.err);
        }

        auto bufferPtr = resultBytes.ok.ptr;
        auto bufferSize = static_cast<std::size_t>(resultBytes.ok.len);
        
        return WriteFileToDisk(filepath, (char const*)bufferPtr, bufferSize);
    }

    Success WriteTextFileToDisk(const std::string& filenameWithExt, const std::string& content, std::string const& serviceName, std::string const& uid)
    {
        assert(fs::path(filenameWithExt).has_extension());

        auto [filename, extension] = SplitFilenameAndExtension(filenameWithExt);
        auto filepath = GenerateFilePath(filename, extension, serviceName, uid);

        return WriteFileToDisk(filepath, content.c_str(), content.size());
    }

    Success WriteFileToDisk(fs::path const& filePath, char const* ptr, std::size_t size)
    {
        std::ofstream file{filePath, std::ios::out | std::ios::binary};

        file.write(ptr, size);
        file.close();

        if (file.fail())
        {
            char message[BufferMaxSize];
            auto errorCode = errno;
#ifdef _WINDOWS
            strerror_s(message, BufferMaxSize, errorCode);
#else
            strerror_r(errorCode, message, BufferMaxSize);
#endif
            return make_error(std::string("Unable to write file on disk: ") + filePath.string() + ". Message (code): " + message + " (" + std::to_string(errorCode) + ")");
        }

        return make_success();
    }

    static std::pair<std::string, std::string> SplitFilenameAndExtension(std::string const& filename)
    {
        fs::path file(filename);
        auto extension = file.extension();
        file.replace_extension();
        return {file.filename().string(), extension.string()};
    }

    fs::path GenerateFilePath(std::string const& filename, std::string const& extension, std::string const& serviceName, std::string const& uid) const
    {
        auto generatedFilename = FileHelper::GenerateFilename(filename, extension, serviceName, uid);

        return _outputDirectory / generatedFilename;
    }

private:
    static constexpr std::size_t BufferMaxSize = 512;

    fs::path _outputDirectory;
};

} // namespace libdatadog