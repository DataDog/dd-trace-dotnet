// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2025 Datadog, Inc.
#pragma once


#include <memory>
#include <vector>

#include "RawSample.h"
#include "Sample.h"

//forward declarations
class IAppDomainStore;
class IFrameStore;
class IRuntimeIdStore;

class RawSampleTransformer
{
public:
    RawSampleTransformer(
        IFrameStore* pFrameStore,
        IAppDomainStore* pAppDomainStore,
        IRuntimeIdStore* pRuntimeIdStore) :
        _pFrameStore{pFrameStore},
        _pAppDomainStore{pAppDomainStore},
        _pRuntimeIdStore{pRuntimeIdStore}
    {
    }

    ~RawSampleTransformer() = default;

    RawSampleTransformer(const RawSampleTransformer&) = delete;
    RawSampleTransformer& operator=(const RawSampleTransformer&) = delete;
    RawSampleTransformer(RawSampleTransformer&&) = delete;
    RawSampleTransformer& operator=(RawSampleTransformer&&) = delete;

    std::shared_ptr<Sample> Transform(const RawSample& rawSample, std::vector<SampleValueTypeProvider::Offset> const& offsets);

    void Transform(const RawSample& rawSample, std::shared_ptr<Sample>& sample, std::vector<SampleValueTypeProvider::Offset> const& offsets);

private:
    void SetAppDomainDetails(const RawSample& rawSample, std::shared_ptr<Sample>& sample);
    void SetThreadDetails(const RawSample& rawSample, std::shared_ptr<Sample>& sample);
    void SetStack(const RawSample& rawSample, std::shared_ptr<Sample>& sample);

    IFrameStore* _pFrameStore;
    IAppDomainStore* _pAppDomainStore;
    IRuntimeIdStore* _pRuntimeIdStore;
};