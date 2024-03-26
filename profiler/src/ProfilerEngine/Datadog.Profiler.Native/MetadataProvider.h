// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IMetadataProvider.h"

#include <tuple>

class MetadataProvider : public IMetadataProvider
{
public:
    static const std::string SectionEnvVars;
    static const std::string DebugLogEnabled;
    static const std::string UploadInterval;
    static const std::string NamedPipeName;
    static const std::string NativeFramesEnabled;
    static const std::string DeprecatedContentionProfilingEnabled;
    static const std::string ExceptionSampleLimit;
    static const std::string AllocationSampleLimit;
    static const std::string ContentionSampleLimit;
    static const std::string CpuWallTimeSamplingRate;
    static const std::string WalltimeThreadsThreshold;
    static const std::string CpuTimeThreadsThreshold;
    static const std::string CodeHotspotsThreadsThreshold;
    static const std::string TimestampsAsLabelEnabled;
    static const std::string UseBacktrace2;
    static const std::string DebugInfoEnabled;
    static const std::string GcThreadsCpuTimeEnabled;
    static const std::string InternalMetricsEnabled;
    static const std::string CoreMinimumOverride;

    static const std::string SectionRuntimeSettings;
    static const std::string NbCores;
    static const std::string CpuLimit;
    static const std::string ClrVersion;
    static const std::string StartTime;

public:
    MetadataProvider();
    ~MetadataProvider() override = default;

    // Inherited via IMetadataProvider
    virtual void Initialize() override;
    virtual void Add(std::string const& section, std::string const& key, std::string const& value) override;
    virtual metadata_t const& Get() override;

private:
    section_t& GetOrAdd(std::string const& section);
    void AddEnvVar(std::string const& section, std::string const& name, shared::WSTRING const& var);

private:
    metadata_t _metadata;
};