// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "MetadataProvider.h"
#include "IConfiguration.h"

const std::string MetadataProvider::ExceptionSampleLimit("ExceptionSampleLimit");
const std::string MetadataProvider::AllocationSampleLimit("AllocationSampleLimit");
const std::string MetadataProvider::ContentionSampleLimit("ContentionSampleLimit");
const std::string MetadataProvider::CpuWallTimeSamplingRate("CpuWallTimeSamplingRate");
const std::string MetadataProvider::WalltimeThreadsThreshold("WalltimeThreadsThreshold");
const std::string MetadataProvider::CpuTimeThreadsThreshold("CpuTimeThreadsThreshold");
const std::string MetadataProvider::CodeHotspotsThreadsThreshold("CodeHotspotsThreadsThreshold");
const std::string MetadataProvider::UseBacktrace2("UseBacktrace2");
const std::string MetadataProvider::DebugInfoEnabled("DebugInfoEnabled");
const std::string MetadataProvider::GcThreadsCpuTimeEnabled("GcThreadsCpuTimeEnabled");
const std::string MetadataProvider::InternalMetricsEnabled("InternalMetricsEnabled");
const std::string MetadataProvider::CoreMinimumOverride("CoreMinimumOverride");
const std::string MetadataProvider::NbCores("NbCores");
const std::string MetadataProvider::CpuLimit("CpuLimit");
const std::string MetadataProvider::ClrVersion("ClrVersion");


MetadataProvider::MetadataProvider()
{
    _metadata.reserve(16);
}

void MetadataProvider::Initialize(IConfiguration* configuration)
{
    Add(ExceptionSampleLimit, std::to_string(configuration->ExceptionSampleLimit()));
    Add(AllocationSampleLimit, std::to_string(configuration->AllocationSampleLimit()));
    Add(ContentionSampleLimit, std::to_string(configuration->ContentionSampleLimit()));

    auto ms = configuration->CpuWallTimeSamplingRate().count() / 1000000;
    Add(CpuWallTimeSamplingRate, std::to_string(ms));
    Add(WalltimeThreadsThreshold, std::to_string(configuration->WalltimeThreadsThreshold()));
    Add(CpuTimeThreadsThreshold, std::to_string(configuration->CpuThreadsThreshold()));
    Add(CodeHotspotsThreadsThreshold, std::to_string(configuration->CodeHotspotsThreadsThreshold()));
    auto boolValue = configuration->UseBacktrace2();
    Add(UseBacktrace2, (boolValue ? "true" : "false"));
    boolValue = configuration->IsDebugInfoEnabled();
    Add(DebugInfoEnabled, (boolValue ? "true" : "false"));
    boolValue = configuration->IsGcThreadsCpuTimeEnabled();
    Add(GcThreadsCpuTimeEnabled, (boolValue ? "true" : "false"));
    boolValue = configuration->IsInternalMetricsEnabled();
    Add(InternalMetricsEnabled, (boolValue ? "true" : "false"));
}

void MetadataProvider::Add(std::string key, std::string value)
{
    _metadata.push_back(std::make_pair(key, value));
}

std::vector<std::pair<std::string, std::string>>& MetadataProvider::Get()
{
    return _metadata;
}
