// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IConfiguration.h"
#include "IGCSuspensionsListener.h"
#include "IGCSuspensionsProvider.h"

#include <mutex>
#include <string>
#include <vector>

class IConfiguration;

class SuspensionInfo
{
public:
    SuspensionInfo(int32_t number, uint32_t generation, uint64_t pauseDuration, uint64_t timestamp);

public:
    int32_t Number;
    uint32_t Generation;
    uint64_t PauseDuration;
    uint64_t Timestamp;
};

class GCSuspensionsProvider
    :
    public IGCSuspensionsProvider,
    public IGCSuspensionsListener
{
public:
    GCSuspensionsProvider(IConfiguration* configuration);
    ~GCSuspensionsProvider();

    virtual bool GetSuspensions(std::stringstream& content) override;
    virtual void OnSuspension(int32_t number, uint32_t generation, uint64_t pauseDuration, uint64_t timestamp) override;

private:
    void SaveSuspensions(std::string& content);
    static void ToJson(std::stringstream& builder, const SuspensionInfo& suspension);

private:
    std::mutex _suspensionsLock;
    std::vector<SuspensionInfo> _suspensions;
    std::filesystem::path _fileFolder;
    uint8_t* _pBuffer;
    uint32_t _current;

};
