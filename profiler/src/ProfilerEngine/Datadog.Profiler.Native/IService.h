// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

class IService
{
public:
    virtual const char* GetName() = 0;
    virtual bool Start() = 0;
    virtual bool Stop() = 0;
    virtual bool IsStarted() = 0;

    virtual ~IService() = default;
};