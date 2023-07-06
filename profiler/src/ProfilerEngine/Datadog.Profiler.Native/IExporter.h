// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once
#include <memory>

// forward declarations
class IProfile;
class IUpscaleProvider;
class IUpscalePoissonProvider;
class ISamplesProvider;
class Sample;

class IExporter
{
public:
    virtual ~IExporter() = default;
    virtual void Add(std::shared_ptr<Sample> const& sample) = 0;
    virtual void SetEndpoint(const std::string& runtimeId, uint64_t traceId, const std::string& endpoint) = 0;
    virtual bool Export() = 0;
    virtual void RegisterUpscaleProvider(IUpscaleProvider* provider) = 0;
    virtual void RegisterUpscalePoissonProvider(IUpscalePoissonProvider* provider) = 0;
    virtual void RegisterProcessSamplesProvider(ISamplesProvider* provider) = 0;
};