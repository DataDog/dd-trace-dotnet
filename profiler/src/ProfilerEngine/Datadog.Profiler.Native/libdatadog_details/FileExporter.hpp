#pragma once

#include <fstream>
#include <string>

#include "OpSysTools.h"

#include "shared/src/native-src/dd_filesystem.hpp"

extern "C"
{
#include "datadog/common.h"
#include "datadog/profiling.h"
}

#define BUFFER_MAX_SIZE 512

namespace libdatadog::detail {
    // this is a Pprof file exporter
    // maybe name it accordingly ?
class FileExporter
{
public:
    FileExporter(fs::path outputDirectory) :
        _outputDirectory{outputDirectory},
        _pid{std::to_string(OpSysTools::GetProcId())}
    {
    }

    error_code WriteToDisk(ddog_prof_EncodedProfile* profile, std::string const& serviceName)
    {
        // TODO move to extension to static field ?
        auto pprofFilePath = GenerateFilePath(serviceName, ".pprof");
        std::ofstream file{pprofFilePath, std::ios::out | std::ios::binary};

        auto buffer = profile->buffer;

        file.write((char const*)buffer.ptr, buffer.len);
        file.close();

        if (file.fail())
        {
            char message[BUFFER_MAX_SIZE];
            auto errorCode = errno;
#ifdef _WINDOWS
            strerror_s(message, BUFFER_MAX_SIZE, errorCode);
#else
            strerror_r(errorCode, message, BUFFER_MAX_SIZE);
#endif
            return detail::make_error(std::string("Unable to write profiles on disk: ") + pprofFilePath + ". Message (code): " + message + " (" + std::to_string(errorCode) + ")");
        }
        //do we want to pass a string ?"Profile serialized in ", pprofFilePath
        return detail::make_success();
    }

    std::string GenerateFilePath(const std::string& applicationName, const std::string& extension) const
    {
        auto time = std::time(nullptr);
        struct tm buf = {};

#ifdef _WINDOWS
        localtime_s(&buf, &time);
#else
        localtime_r(&time, &buf);
#endif

        std::stringstream oss;
        // TODO: review the way we compute the differentiator number: OpSysTools::GetHighPrecisionNanoseconds() % 10000
        oss << applicationName + "_" << _pid << "_" << std::put_time(&buf, "%F_%H-%M-%S") << "_" << (OpSysTools::GetHighPrecisionNanoseconds() % 10000)
            << extension;
        auto pprofFilename = oss.str();

        auto pprofFilePath = _outputDirectory / pprofFilename;

        return pprofFilePath.string();
    }

private:
    fs::path _outputDirectory;
    std::string _pid;
};
} // namespace libdatadog::detail