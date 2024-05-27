// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ServiceBase.h"

ServiceBase::ServiceBase() :
    _currentState{ServiceState::Init}
{
}

bool ServiceBase::Start()
{
    auto expected = ServiceState::Init;
    auto exchange = _currentState.compare_exchange_strong(expected, ServiceState::Starting);

    if (!exchange)
    {
        return false;
    }

    auto result = StartImpl();
    if (result)
    {
        _currentState = ServiceState::Started;
    }
    else
    {
        _currentState = ServiceState::Init;
    }

    return result;
}

bool ServiceBase::Stop()
{
    auto expected = ServiceState::Started;
    auto exchange = _currentState.compare_exchange_strong(expected, ServiceState::Stopping);

    if (!exchange)
    {
        return false;
    }

    auto result = StopImpl();
    if (result)
    {
        _currentState = ServiceState::Stopped;
    }
    else
    {
        _currentState = ServiceState::Started;
    }

    return result;
}
