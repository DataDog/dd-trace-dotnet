// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IMetadataProvider.h"

#include <tuple>

class MetadataProvider : public IMetadataProvider
{
public:
    static const std::string SectionEnvVars;
    static const std::string ExceptionSampleLimit;
    static const std::string AllocationSampleLimit;
    static const std::string ContentionSampleLimit;
    static const std::string CpuWallTimeSamplingRate;
    static const std::string WalltimeThreadsThreshold;
    static const std::string CpuTimeThreadsThreshold;
    static const std::string CodeHotspotsThreadsThreshold;
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
    virtual void Initialize(IConfiguration* configuration) override;
    virtual void Add(std::string section, std::string key, std::string value) override;
    virtual std::vector<std::pair<std::string, std::vector<std::pair<std::string, std::string>>>>& Get() override;

private:
    std::pair<std::string, std::vector<std::pair<std::string, std::string>>>& GetOrAdd(std::string section);
    void AddEnvVar(std::string section, std::string name, shared::WSTRING var);
    bool GetEnvVar(shared::WSTRING name, std::string& value);

        private:
    std::vector<std::pair<std::string, std::vector<std::pair<std::string, std::string>>>> _metadata;
};