// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
#pragma once

#include "IService.h"

#include <atomic>
#include <string>

class ServiceBase : public IService
{
public:
    ServiceBase();

    bool Start() final override;
    bool Stop() final override;

protected:
    virtual bool StartImpl() = 0;
    virtual bool StopImpl() = 0;

    enum class State
    {
        Init,
        Starting,
        Started,
        Stopping,
        Stopped
    };

    State GetState() const;

private:

    friend std::string to_string(ServiceBase::State);
    std::atomic<State> _currentState;
};
