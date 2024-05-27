// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ServiceBase.h"

ServiceBase::ServiceBase() :
    _currentState{State::Init}
{
}

bool ServiceBase::Start()
{
    auto expected = State::Init;
    auto exchange = _currentState.compare_exchange_strong(expected, State::Starting);

    if (!exchange)
    {
        return false;
    }

    auto result = StartImpl();
    if (result)
    {
        _currentState = State::Started;
    }
    else
    {
        _currentState = State::Init;
    }

    return result;
}

bool ServiceBase::Stop()
{
    auto expected = State::Started;
    auto exchange = _currentState.compare_exchange_strong(expected, State::Stopping);

    if (!exchange)
    {
        return false;
    }

    auto result = StopImpl();
    if (result)
    {
        _currentState = State::Stopped;
    }
    else
    {
        _currentState = State::Started;
    }

    return result;
}
