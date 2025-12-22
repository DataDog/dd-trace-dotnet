// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MetadataProvider.h"
#include "OsSpecificApi.h"
#include "IConfiguration.h"

#include "EnvironmentVariables.h"
#include "shared/src/native-src/util.h"


const std::string MetadataProvider::SectionEnvVars("Environment Variables");
const std::string MetadataProvider::DebugLogEnabled("DD_TRACE_DEBUG"); // could be an issue if enabled
const std::string MetadataProvider::UploadInterval("DD_PROFILING_UPLOAD_PERIOD");  // could explain weird upload frequencies
const std::string MetadataProvider::NamedPipeName("DD_TRACE_PIPE_NAME");  // could explain issues on Linux
const std::string MetadataProvider::NativeFramesEnabled("DD_PROFILING_FRAMES_NATIVE_ENABLED");     // should be always disabled
const std::string MetadataProvider::DeprecatedContentionProfilingEnabled("DD_PROFILING_CONTENTION_ENABLED"); // deprecated
const std::string MetadataProvider::ExceptionSampleLimit("DD_INTERNAL_PROFILING_EXCEPTION_SAMPLE_LIMIT");
const std::string MetadataProvider::AllocationSampleLimit("DD_INTERNAL_PROFILING_ALLOCATION_SAMPLE_LIMIT");
const std::string MetadataProvider::ContentionSampleLimit("DD_INTERNAL_PROFILING_CONTENTION_SAMPLE_LIMIT");
const std::string MetadataProvider::CpuWallTimeSamplingRate("DD_INTERNAL_PROFILING_SAMPLING_RATE");
const std::string MetadataProvider::WalltimeThreadsThreshold("DD_INTERNAL_PROFILING_WALLTIME_THREADS_THRESHOLD");
const std::string MetadataProvider::CpuTimeThreadsThreshold("DD_INTERNAL_PROFILING_CPUTIME_THREADS_THRESHOLD");
const std::string MetadataProvider::CodeHotspotsThreadsThreshold("DD_INTERNAL_PROFILING_CODEHOTSPOTS_THREADS_THRESHOLD");
const std::string MetadataProvider::TimestampsAsLabelEnabled("DD_INTERNAL_PROFILING_TIMESTAMPS_AS_LABEL_ENABLED"); // should never be 0
const std::string MetadataProvider::CoreMinimumOverride("DD_PROFILING_MIN_CORES_THRESHOLD");
const std::string MetadataProvider::DebugInfoEnabled("DD_INTERNAL_PROFILING_DEBUG_INFO_ENABLED");
const std::string MetadataProvider::GcThreadsCpuTimeEnabled("DD_INTERNAL_GC_THREADS_CPUTIME_ENABLED");
const std::string MetadataProvider::InternalMetricsEnabled("DD_INTERNAL_METRICS_ENABLED");

const std::string MetadataProvider::SectionRuntimeSettings("Runtime Settings");
const std::string MetadataProvider::StartTime("Start Time");
const std::string MetadataProvider::NbCores("Number of Cores");
const std::string MetadataProvider::CpuLimit("Cpu Limit");
const std::string MetadataProvider::ClrVersion("Clr Version");


MetadataProvider::MetadataProvider()
{
    _metadata.reserve(16);
}

void MetadataProvider::Initialize()
{
    AddEnvVar(SectionEnvVars, ExceptionSampleLimit, EnvironmentVariables::ExceptionSampleLimit);
    AddEnvVar(SectionEnvVars, AllocationSampleLimit, EnvironmentVariables::AllocationSampleLimit);
    AddEnvVar(SectionEnvVars, ContentionSampleLimit, EnvironmentVariables::ContentionSampleLimit);
    AddEnvVar(SectionEnvVars, ContentionSampleLimit, EnvironmentVariables::ContentionSampleLimit);
    AddEnvVar(SectionEnvVars, WalltimeThreadsThreshold, EnvironmentVariables::WalltimeThreadsThreshold);
    AddEnvVar(SectionEnvVars, CpuTimeThreadsThreshold, EnvironmentVariables::CpuTimeThreadsThreshold);
    AddEnvVar(SectionEnvVars, CodeHotspotsThreadsThreshold, EnvironmentVariables::CodeHotspotsThreadsThreshold);
    AddEnvVar(SectionEnvVars, DebugInfoEnabled, EnvironmentVariables::DebugInfoEnabled);
    AddEnvVar(SectionEnvVars, GcThreadsCpuTimeEnabled, EnvironmentVariables::GcThreadsCpuTimeEnabled);
    AddEnvVar(SectionEnvVars, InternalMetricsEnabled, EnvironmentVariables::InternalMetricsEnabled);
    AddEnvVar(SectionEnvVars, CpuWallTimeSamplingRate, EnvironmentVariables::CpuWallTimeSamplingRate);

    auto st = OsSpecificApi::GetProcessStartTime();
    if (!st.empty())
    {
        Add(SectionRuntimeSettings, StartTime, st);
    }
}

void MetadataProvider::Add(std::string const& section, std::string const& key, std::string const& value)
{
    auto& element = GetOrAdd(section);
    element.second.push_back(std::make_pair(key, value));
}

MetadataProvider::metadata_t const& MetadataProvider::Get()
{
    return _metadata;
}

MetadataProvider::section_t& MetadataProvider::GetOrAdd(std::string const& section)
{
    for (auto& part : _metadata)
    {
        if (part.first == section)
        {
            return part;
        }
    }

    _metadata.push_back(section_t(section, std::vector<std::pair<std::string, std::string>>()));
    return _metadata[_metadata.size()-1];
}

void MetadataProvider::AddEnvVar(std::string const& section, std::string const& name, shared::WSTRING const& var)
{
    auto value = shared::GetEnvironmentValue(var);
    if (!value.empty())
    {
        Add(section, name, shared::ToString(value));
    }
}